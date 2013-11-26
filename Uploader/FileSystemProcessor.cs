using System;
using System.Collections.Generic;
using System.IO;
using ProtectionServer;
using SiteController;

namespace Uploader
{
    /// <summary>
    /// ���������� IFileProcessor ��� ������ � ������� �������
    /// ���������� ������ �������� ��� ������ � ������ � ���������� ��� � �������� �������
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
        /// ��������� ���� ���������� �����
        /// </summary>
        public string OutputPath
        {
            get { return m_outputPath; }
            set
            {
                // � �������� ����� ��������� ���� ������, ���� ����������� �� �������
                // ����� ���� � ������������ ������ ������� ���� ������, �������� �� ���������� �����������
                //if (!Directory.Exists(value))
                //    throw new ArgumentException("Directory does not exist:" + value);
                //_outputPath = value;
                m_outputPath = CApplConfigurationManager.GetApplSetting("LocalDownLoadDir");
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// �������������� � ����� ��� ���������� ������
        /// </summary>
        public FileSystemProcessor()
        {
            m_outputPath = CApplConfigurationManager.GetApplSetting("LocalDownLoadDir");
        }

        #endregion

        #region IFileProcessor Members

        /// <summary>
        /// �������� ��������� ����
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="contentType"></param>
        /// <param name="headerItems"></param>
        /// <returns>� �������� ���� ����� - ��� �� �������</returns>
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
                // ��� ��� �������
                throw ex;
            }

            return m_strfileName;
        }

        /// <summary>
        /// ������ ������ � ����
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
        /// ���������� ���������� �����
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
        /// ���� ���� ������� �����������, ��������� ����� � �������
        /// </summary>
        public void DestroyFile()
        {
            EndFile();
            File.Delete(m_outputPath + m_strfileName);
        }

        /// <summary>
        /// ������ ��� �����
        /// </summary>
        /// <returns></returns>
        public string GetFileName()
        {
            return m_strfileName;
        }

        /// <summary>
        /// ������ �������������, ��� ����� ��� ���
        /// </summary>
        /// <returns></returns>
        public object GetIdentifier()
        {
            return m_strfileName;
        }

        /// <summary>
        /// ��������� �������
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