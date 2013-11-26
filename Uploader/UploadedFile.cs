using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Uploader
{
    /// <summary>
    /// Данные загружаемого файла
    /// </summary>
    public class UploadedFile
    {
        #region vars

        readonly string m_fileName;
        readonly object m_identifier;
        readonly Dictionary<string, string> m_headerItems;
        readonly Exception m_exception;

        #endregion

        #region Properties

        /// <summary>
        /// имя файла
        /// </summary>
        public string FileName
        {
            get { return m_fileName; }
        }

        /// <summary>
        /// идентификатор от процессора
        /// </summary>
        public object Identifier
        {
            get { return m_identifier; }
        }

        /// <summary>
        /// заголовки запроса
        /// </summary>
        public Dictionary<string, string> HeaderItems
        {
            get { return m_headerItems; }
        }

        /// <summary>
        /// Exception
        /// </summary>
        public Exception Exception
        {
            get { return m_exception; }
        }

        #endregion

        #region Constructor


        public UploadedFile(string fileName, object identifier, Dictionary<string, string> headerItems)
        {
            m_fileName = fileName;
            m_identifier = identifier;
            m_headerItems = headerItems;
        }

        // если исключение создаем так
        public UploadedFile(string fileName, object identifier, Dictionary<string, string> headerItems, Exception ex) : this(fileName, identifier, headerItems)
        {
            m_exception = ex;
        }

        #endregion
    }
}
