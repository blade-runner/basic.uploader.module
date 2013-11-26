using System;
using System.Globalization;
using System.Reflection;
using System.Security.Permissions;
using System.Threading;
using System.Web;
using System.Web.Configuration;

namespace Uploader
{
    /// <summary>
    /// Модуль сохраняет файлы из стрима, передавая его FormProcessor а тот сохраняет с помощью IFileProcessor 
    /// сейчас сделано сохранения в файловую систему
    /// </summary>
    public class UploadModule : IHttpModule
    {
        #region Declarations

        private const string C_MARKER = "multipart/form-data; boundary=";
        private const string B_MARKER = "boundary=";
        private IFileProcessor m_processor;

        #endregion

        #region Properties

        public UploadStatus Status { get; private set; }

        #endregion

        #region Constructor

        // не используется

        #endregion

        #region IHttpModule Members

        public void Init(HttpApplication context)
        {
            context.BeginRequest += Context_AuthenticateRequest;
        }

        public void Dispose()
        {
        }

        #endregion

        #region Event handlers

        /// <summary>
        /// вся обработка после аутентификации. Позволяет получить доступ к security context если нужно.
        /// Подгружаем заголовки и инициализируем стрим
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Context_AuthenticateRequest(object sender, EventArgs e)
        {
            var app = sender as HttpApplication;
            HttpWorkerRequest worker = GetWorkerRequest(app.Context);
            int bufferSize;
            string boundary;
            string ct;
            bool statusPersisted = false;

            UploadManager.Instance.ModuleInstalled = true;

            bufferSize = UploadManager.Instance.BufferSize;

            ct = worker.GetKnownRequestHeader(HttpWorkerRequest.HeaderContentType);

            var qwe = worker.GetQueryString();

            // application/x-www-form-urlencoded
            // проверка что это именно форма с файлом. еще и параметр запроса проверяем
            if (worker.GetQueryString() == "upload" && ct != null /*&& string.Compare(ct, 0, C_MARKER, 0, C_MARKER.Length, true, CultureInfo.InvariantCulture) == 0*/)
            {
                long length = long.Parse(worker.GetKnownRequestHeader(HttpWorkerRequest.HeaderContentLength));
               // long length = HttpContext.Current.Request.ContentLength;

                if (length > 0)
                {
                    if (length/1024 > GetMaxRequestLength(app.Context))
                    {

                      InitOverlodedStatus();
                       // return;
                      //  throw new Exception("Размер запроса превышен");
                    }
                    else
                    {
                        InitStatus(length);
                    }

                    boundary = "--" + ct.Substring(ct.IndexOf(B_MARKER) + B_MARKER.Length);
                   // InitStatus(length);

                    using (var fs = new FormStream(GetProcessor(), boundary, app.Request.ContentEncoding))
                    {
                        // Set up events
                        fs.FileCompleted += fs_FileCompleted;
                        fs.FileCompletedError += fs_FileCompletedError;
                        fs.FileStarted += fs_FileStarted;

                        byte[] data = null;
                        int read = 0;
                        int counter = 0;

                        if (worker.GetPreloadedEntityBodyLength() > 0)
                        {
                            
                            data = worker.GetPreloadedEntityBody();

                            fs.Write(data, 0, data.Length);

                            if (!String.IsNullOrEmpty(fs.StatusKey))
                            {
                                if (!statusPersisted) PersistStatus(fs.StatusKey);
                                statusPersisted = true;
                                Status.UpdateBytes(data.Length, m_processor.GetFileName(), m_processor.GetIdentifier());
                            }

                            counter = data.Length;
                        }

                        bool disconnected = false;

                        // Read todo: в отдельную функцию  
                        while (counter < length && worker.IsClientConnected() && !disconnected)
                        {
                            if (counter + bufferSize > length)
                            {
                                bufferSize = (int) length - counter;
                            }

                            data = new byte[bufferSize];
                            read = worker.ReadEntityBody(data, bufferSize);
                            if (read > 0)
                            {
                                Thread.Sleep(100);
                                counter += read;
                                fs.Write(data, 0, read);

                                if (!String.IsNullOrEmpty(fs.StatusKey))
                                {
                                    if (!statusPersisted) PersistStatus(fs.StatusKey);
                                    statusPersisted = true;
                                    Status.UpdateBytes(counter, m_processor.GetFileName(), m_processor.GetIdentifier());
                                }
                            }
                            else
                            {
                                disconnected = true;
                            }
                        }

                        if (!worker.IsClientConnected() || disconnected)
                        {
                            m_processor.DestroyFile();
                            app.Context.Response.End();
                            return;
                        }

                        if (fs.ContentMinusFiles != null)
                        {
                            BindingFlags ba = BindingFlags.Instance | BindingFlags.NonPublic;

                            // заменяем workerRequest
                            var wr = new UploadWorkerRequest(worker, fs.ContentMinusFiles);
                            app.Context.Request.GetType().GetField("_wr", ba).SetValue(app.Context.Request, wr);
                        }

                   //todo:     app.Context.Items["projectName"] = worker.get
                        app.Context.Items["uplFileName"] = fs.Identifier;
                        app.Context.Items[FormConsts.STATUS_KEY] = fs.StatusKey;
                    }
                }
            }
        }

        /// <summary>
        /// апдейт статуса если начало загузки файла
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        private void fs_FileStarted(object sender, string fileName, object identifier)
        {
            Status.UpdateFile(fileName, identifier);
        }

        /// <summary>
        /// если есть данные об ошибке при загрузке
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        /// <param name="ex"></param>
        private void fs_FileCompletedError(object sender, string fileName, object identifier, Exception ex)
        {
            Status.ErrorFiles.Add(new UploadedFile(fileName, identifier, m_processor.GetHeaderItems(), ex));
        }

        /// <summary>
        /// файл закончен
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        private void fs_FileCompleted(object sender, string fileName, object identifier)
        {
            Status.UploadedFiles.Add(new UploadedFile(fileName, identifier, m_processor.GetHeaderItems()));
        }

        #endregion

        #region Methods

        /// <summary>
        /// получаем текущий workerRequest
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private HttpWorkerRequest GetWorkerRequest(HttpContext context)
        {
            IServiceProvider provider = context;

            // если траблы с доступом - используем
        	new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Assert();
            return (HttpWorkerRequest) provider.GetService(typeof (HttpWorkerRequest));
        }

        /// <summary>
        /// какой процесор используем
        /// </summary>
        /// <returns></returns>
        private IFileProcessor GetProcessor()
        {
            m_processor = UploadManager.Instance.GetProcessor();
            return m_processor;
        }

        /// <summary>
        /// статус для новой загрузки
        /// </summary>
        /// <param name="length"></param>
        private void InitStatus(long length)
        {
            Status = new UploadStatus(length);
        }

        private void InitOverlodedStatus()
        {
            Status = new UploadStatus(true);
        }

        /// <summary>
        /// Сохранение статуса
        /// </summary>
        /// <param name="key"></param>
        private void PersistStatus(string key)
        {
            UploadManager.Instance.SetStatus(Status, key);
        }

        /// <summary>
        /// Максимальный размер запроса (в кб)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private int GetMaxRequestLength(HttpContext context)
        {
            int DEFAULT_MAX = 1024;
            var config = context.GetSection("system.web/httpRuntime") as HttpRuntimeSection;
            return config == null ? DEFAULT_MAX : config.MaxRequestLength;
        }


        #endregion
    }
}