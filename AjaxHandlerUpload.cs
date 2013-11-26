using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using ProtectionServer.Classes;
using Resources;
using SiteController;
using SiteTransferData;
using Uploader;

namespace ProtectionServer
{
    public partial class AjaxHandler
    {


    

        /// <summary>
        /// отдаем процент загрузки
        /// </summary>
        public void GetProgress()
        {
            UploadStatus status = UploadManager.Instance.Status;
            m_message = new CMessageXml(status ?? UploadStatus.EMPTY_STATUS);
        }

        /// <summary>
        /// Обрабатываем загружаемый файл
        /// </summary>
        private void SavePostData()
        {
            m_message = new CMessageXml(string.Empty, CMessageXml.EMessageXmlType.Error);
            try
            {
                if (CheckFiles())
                {
                    SaveUplodedFile(m_context.Items["uplFileName"].ToString());
                  

                    m_message = nOrderID == nOrderErrorId ? new CMessageXml(CResourceManager.GetErrorMessage(StrErrorMessage), CMessageXml.EMessageXmlType.Error) : 
                        arrStat.Count > 0 ?
                        new CMessageXml(String.Empty, GetOrderLine()) : new CMessageXml(string.Empty,new sOrderRow
                          {
                            oID = nOrderID.ToString(),
                            oDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                            oStatus = LocalizedText.eWaitInQueue,
                            oReady = "в процессе"//CResourceManager.MakeEstimatedTimeCaption(((COrderStatistic)row).m_nEvalTimeLeft),
                          });
                }
            }
            catch (HttpException httpEx)
            {
                m_message = new CMessageXml(httpEx.Message, CMessageXml.EMessageXmlType.Error);
            }

            m_context.Response.Write(m_message.Serialize());
        }



        /// <summary>
        /// Отдает данные для добавления нового заказа в табличку заказов пользователя
        /// </summary>
        private sOrderRow GetOrderLine()
        {
           
            var row = arrStat[arrStat.Count - 1];
            var ret = new sOrderRow
                          {
                            oID = nOrderID.ToString(),
                            oDate = DateTime.FromBinary(((COrderStatistic)row).m_nAcceptDate).ToString("dd.MM.yyyy HH:mm"),
                            oStatus = LocalizedText.eWaitInQueue,
                            oReady = CResourceManager.MakeEstimatedTimeCaption(((COrderStatistic)row).m_nEvalTimeLeft),
                          };
       
            return ret;
        }


        /// <summary>
        /// проверяем что подгрузил пользователь
        /// </summary>
        private bool CheckFiles()
        {
            if (!m_context.User.Identity.IsAuthenticated)
            {
                m_message = new CMessageXml(CResourceManager.GetErrorMessage("ordSessionEndError"), CMessageXml.EMessageXmlType.Auth);
                return false;
            }
            if (m_context.Request.Files.Count != 0)
            {
                var zipreg = new Regex(StrZipRegex);
                if (zipreg.IsMatch(m_context.Request.Files[0].FileName))
                {
                    return true;
                }
                m_message = new CMessageXml(CResourceManager.GetErrorMessage("ordWrongFileType"), CMessageXml.EMessageXmlType.Error);
            }
            else
            {
                m_message = new CMessageXml(CResourceManager.GetErrorMessage("ordWrongFileType"), CMessageXml.EMessageXmlType.Error);
            }
            return false;
        }


          /// <summary>
        ///  Сохранием  файл на диске и в базе
        /// </summary>
        /// <param name="filename">имя файла</param>
        protected void SaveUplodedFile(string filename)
        {

            ISiteController dbController = CDbServerAccessor.GetDbServerInterface();

            try
            {
                
                //отдаем на сервер только имя клиента, путь к файлу архива. оригинальное имя файла архива пока никчему.
                string strFileNameOnSite =
                    CApplConfigurationManager.GetDirNameForExtended("InputFilesShareName", "LocalDownLoadDir") +
                    filename;

                int userid = CDbServerAccessor.GetCurrentClientIDFromSession();

                // todo: nikita!
                nOrderID = dbController.CreateNewOrderForProject(userid, strFileNameOnSite, Classes.Helper.CommonFunctions.GetClearedProjectName(m_context.Request.Form["projname"]));
                
                dbController.GetOrdersStatisticForClient(//HttpContext.Current.User.Identity.Name,
                    userid,
                                                         DateTime.Now.AddMinutes(-10).ToBinary(),
                                                         DateTime.Now.AddSeconds(5).ToBinary(),
                                                         EOrderStatus.eAllStatus,
                                                         ref arrStat);
            }
            catch (Exception e)
            {
               
                if (e is CAProtException)
                {
                    StrErrorMessage = e.Message;
                }
                else
                {
                    StrErrorMessage = e.Source;
                }
                nOrderID = -1;
            }
        }

        /// <summary>
        ///  Сохранием  файл на диске и в базе 
        /// </summary>
        [Obsolete("заменен на новый метод, использующий имя файла и файло, сохраненное httpworkerrequest")]
        protected void SaveUplodedFile()
        {
            HttpFileCollection files = m_context.Request.Files;

            ISiteController dbController = CDbServerAccessor.GetDbServerInterface();
            string strInternalName = String.Empty;

            // получаем имя директории и guid для сохранения.
            dbController.CreateInternalNameForFile(files[0].FileName, ref strInternalName);
            string strLocUplFileName = CApplConfigurationManager.GetApplSetting("LocalDownLoadDir") + strInternalName;

           //    files[0].SaveAs(strLocUplFileName);
            SaveStreamToDisk(strLocUplFileName, files);
            try
            {
                //отдаем на сервер только имя клиента, путь к файлу архива. оригинальное имя файла архива пока никчему.
                string strFileNameOnSite =
                    CApplConfigurationManager.GetDirNameForExtended("InputFilesShareName", "LocalDownLoadDir") +
                    strInternalName;

                int userid = CDbServerAccessor.GetCurrentClientIDFromSession();

                nOrderID = dbController.CreateNewOrderForProject(userid, strFileNameOnSite, "");

                dbController.GetOrdersStatisticForClient(//HttpContext.Current.User.Identity.Name,
                    CDbServerAccessor.GetCurrentClientIDFromSession(),
                                                         DateTime.Now.AddSeconds(-3).ToBinary(),
                                                         DateTime.Now.AddSeconds(3).ToBinary(),
                                                         EOrderStatus.eAllStatus,
                                                         ref arrStat);

            }
            catch (Exception e)
            {
                if (e is CAProtException)
                {
                    StrErrorMessage = e.Message;
                }
                else
                {
                    StrErrorMessage = e.Source;
                }
                nOrderID = -1;
            }
        }

        /// <summary>
        /// сохраняем файл, используя стрим. Позволит отдавать процент загрузки
        /// </summary>
        /// <param name="strLocUplFileName"></param>
        /// <param name="files"></param>
        private static void SaveStreamToDisk(string strLocUplFileName, HttpFileCollection files)
        {
            throw new Exception("implement");
            //using (FileStream output = new FileStream(strLocUplFileName, FileMode.Create, FileAccess.Write))
            //{
            //    var length = files[0].InputStream.Length;
            //    byte[] buffer = new byte[MaxBufferSize];
            //    int bufferSize = 0;
            //    do
            //    {
            //        bufferSize = files[0].InputStream.Read(buffer, 0, MaxBufferSize);
            //        if (bufferSize != 0)
            //        {
            //            output.Write(buffer, 0, bufferSize);

            //            //HttpRuntime.Cache.Insert("UplProc", files[0].InputStream.Position * 100 / length, null, DateTime.UtcNow.AddSeconds(3),
            //            //                Cache.NoSlidingExpiration,
            //            //                CacheItemPriority.Normal, null);

            //            //       string sd = HttpRuntime.Cache["UplProc"].ToString();
            //            //    m_context.Session["UploadPrc"] = files[0].InputStream.Position * 100 / length;
            //        }
            //        // Thread.Sleep(500);
            //    } while (bufferSize != 0);
            //}
        }
    }
}