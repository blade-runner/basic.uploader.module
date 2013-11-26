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
    /// ������ ��������� ����� �� ������, ��������� ��� FormProcessor � ��� ��������� � ������� IFileProcessor 
    /// ������ ������� ���������� � �������� �������
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

        // �� ������������

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
        /// ��� ��������� ����� ��������������. ��������� �������� ������ � security context ���� �����.
        /// ���������� ��������� � �������������� �����
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
            // �������� ��� ��� ������ ����� � ������. ��� � �������� ������� ���������
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
                      //  throw new Exception("������ ������� ��������");
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

                        // Read todo: � ��������� �������  
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

                            // �������� workerRequest
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
        /// ������ ������� ���� ������ ������� �����
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        private void fs_FileStarted(object sender, string fileName, object identifier)
        {
            Status.UpdateFile(fileName, identifier);
        }

        /// <summary>
        /// ���� ���� ������ �� ������ ��� ��������
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
        /// ���� ��������
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
        /// �������� ������� workerRequest
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private HttpWorkerRequest GetWorkerRequest(HttpContext context)
        {
            IServiceProvider provider = context;

            // ���� ������ � �������� - ����������
        	new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Assert();
            return (HttpWorkerRequest) provider.GetService(typeof (HttpWorkerRequest));
        }

        /// <summary>
        /// ����� �������� ����������
        /// </summary>
        /// <returns></returns>
        private IFileProcessor GetProcessor()
        {
            m_processor = UploadManager.Instance.GetProcessor();
            return m_processor;
        }

        /// <summary>
        /// ������ ��� ����� ��������
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
        /// ���������� �������
        /// </summary>
        /// <param name="key"></param>
        private void PersistStatus(string key)
        {
            UploadManager.Instance.SetStatus(Status, key);
        }

        /// <summary>
        /// ������������ ������ ������� (� ��)
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