using System;
using System.Collections.Generic;
using System.IO;
using ProtectionServer;
using SiteController;

namespace Uploader
{
    /// <summary>
    /// реализация IFileProcessor для записи в фаловую систему
    /// передается модулю загрузки для работы с файлом и сохранению его в файловой системе
    /// </summary>
    [Serializable]
    public class FileSystemProcessor : IFileProcessor
    {
        #region Declarations

        [NonSerialized] private bool m_berrorState;
        [NonSerialized] private string m_strfullFileName = String.Empty;
        [NonSerialized] private Dictionary<string, string> m_headerItems;
        [NonSerialized] private string m_strfileName;
        [NonSerialized] private FileStream m_fs;

        private string m_outputPath;

        #endregion

        #region Properties

        /// <summary>
        /// Установка пути сохранения файла
        /// </summary>
        public string OutputPath
        {
            get { return m_outputPath; }
            set
            {
                // в принципе можно сохранять куда угодно, пока прописываем из конфига
                // кроме того в конструкторе сейчас такойже путь ставим, свойство на расширение функционала
                //if (!Directory.Exists(value))
                //    throw new ArgumentException("Directory does not exist:" + value);
                //_outputPath = value;
                m_outputPath = CApplConfigurationManager.GetApplSetting("LocalDownLoadDir");
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Инициализируем с путем для сохранения файлов
        /// </summary>
        public FileSystemProcessor()
        {
            m_outputPath = CApplConfigurationManager.GetApplSetting("LocalDownLoadDir");
        }

        #endregion

        #region IFileProcessor Members

        /// <summary>
        /// начинаем сохранять файл
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="contentType"></param>
        /// <param name="headerItems"></param>
        /// <returns>в качестве айди файла - имя на сервере</returns>
        public object StartNewFile(string fileName, string contentType, Dictionary<string, string> headerItems)
        {
            m_berrorState = false;
            m_headerItems = headerItems;
            try
            {
                ISiteController dbController = CDbServerAccessor.GetDbServerInterface();
                dbController.CreateInternalNameForFile(Path.GetFileName(fileName), ref m_strfileName);
                m_strfullFileName = m_outputPath + m_strfileName;
                m_fs = new FileStream(m_strfullFileName, FileMode.Create);
            }
            catch (Exception ex)
            {
                m_berrorState = true;
                // еще раз бросаем
                throw ex;
            }

            return m_strfileName;
        }

        /// <summary>
        /// Запись данных в файл
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (m_berrorState) return;
            try
            {
                m_fs.Write(buffer, offset, count);
            }
            catch (Exception ex)
            {
                m_berrorState = true;
                throw ex;
            }
        }

        /// <summary>
        /// Завершение сохранения файла
        /// </summary>
        public void EndFile()
        {
            if (m_berrorState) return;

            if (m_fs != null)
            {
                m_fs.Flush();
                m_fs.Close();
                m_fs.Dispose();
            }
        }

        /// <summary>
        /// если файл закачан неполностью, закрываем поток и удаляем
        /// </summary>
        public void DestroyFile()
        {
            EndFile();
            File.Delete(m_outputPath + m_strfileName);
        }

        /// <summary>
        /// отдаем имя файла
        /// </summary>
        /// <returns></returns>
        public string GetFileName()
        {
            return m_strfileName;
        }

        /// <summary>
        /// отдаем идентификатор, для файла его имя
        /// </summary>
        /// <returns></returns>
        public object GetIdentifier()
        {
            return m_strfileName;
        }

        /// <summary>
        /// Заголовки запроса
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetHeaderItems()
        {
            return m_headerItems;
        }

        /// <summary>
        /// Dispose 
        /// </summary>
        void IDisposable.Dispose()
        {
            if (m_fs != null)
            {
                m_fs.Dispose();
            }
        }

        #endregion
    }
}