using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Uploader
{
    /// <summary>
    /// делегат на события с файлом
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="fileName"></param>
    /// <param name="identifier"></param>
    public delegate void FileEventHandler(object sender, string fileName, object identifier);

    /// <summary>
    /// делегат при ошибке
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="fileName"></param>
    /// <param name="identifier"></param>
    /// <param name="ex"></param>
    public delegate void FileErrorEventHandler(object sender, string fileName, object identifier, Exception ex);

    /// <summary>
    /// Стрим для парсинга HTTP uload (RFC1867) запроса
    /// пишет в IFileProcessor
    /// </summary>
    internal class FormStream : Stream, IDisposable
    {
        #region Declarations

        private IFileProcessor m_processor;
        private IFileProcessor m_defaultProcessor;
        private readonly Encoding m_encoding;
        private readonly MemoryStream m_formContent;
        private MemoryStream m_currentField;
        private long m_position;
        private readonly int m_keepBackLength;
        private bool m_fileError;
        private Exception m_ex;
        private string m_fileName;
        private string m_statusKey = String.Empty;

        // переменные для работы с заголовками и запросом на уровне workerRequest
        private readonly byte[] EOF;
        private readonly byte[] BOUNDARY;
        private readonly byte[] EOH;
        private byte[] CRLF; // на всякий случай
        private readonly byte[] ID_TAG;
        private readonly byte[] DEFAULT_PARAMS_TAG;
        private readonly byte[] PARAMS_TAG;
        private readonly byte[] END_TAG;

        private object m_id;

        #endregion

        #region Events

        /// <summary>
        /// Ошибка
        /// </summary>
        public event ErrorEventHandler Error;

        /// <summary>
        /// Когда евент ошибки
        /// </summary>
        /// <param name="ex"></param>
        protected void OnError(Exception ex)
        {
            m_ex = ex;

            if (Error != null)
            {
                Error(this, new ErrorEventArgs(ex));
            }
        }


        /// <summary>
        /// Начало новго файла
        /// </summary>
        public event FileEventHandler FileStarted;

        /// <summary>
        /// когда начало новго файла
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        protected void OnFileStarted(string fileName, object identifier)
        {
            if (FileStarted != null)
                FileStarted(this, fileName, identifier);
        }

        /// <summary>
        /// Успешное завершение загрузки
        /// </summary>
        public event FileEventHandler FileCompleted;

        /// <summary>
        /// при успешном завершении загрузки
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        protected void OnFileCompleted(string fileName, object identifier)
        {
            if (FileCompleted != null)
                FileCompleted(this, fileName, identifier);
        }

        /// <summary>
        /// Когда ошибка
        /// </summary>
        public event FileErrorEventHandler FileCompletedError;

        /// <summary>
        /// При ошибке загрузки
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        /// <param name="ex"></param>
        protected void OnFileCompletedError(string fileName, object identifier, Exception ex)
        {
            if (FileCompletedError != null)
                FileCompletedError(this, fileName, identifier, ex);
        }

        #endregion

        #region SectionResult

        /// <summary>
        /// Результат обработки куска файла
        /// </summary>
        private class SectionResult
        {
            #region Declarations

            private readonly SectionAction m_nextAction;
            private readonly int m_nextOffset;

            #endregion

            // что делаем после загрузки куска
            public enum SectionAction
            {
                BoundaryReached, // текущий баундари закончился
                NoBoundaryKeepBuffer // нет части, дальше грузим из буфера
            }

            /// <summary>
            /// что дальше делаем 
            /// </summary>
            public SectionAction NextAction
            {
                get { return m_nextAction; }
            }

            /// <summary>
            /// Следующий офсет
            /// </summary>
            public int NextOffset
            {
                get { return m_nextOffset; }
            }

            public SectionResult(int nextOffset, SectionAction nextAction)
            {
                m_nextAction = nextAction;
                m_nextOffset = nextOffset;
            }
        }

        #endregion

        #region Public properties

        /// <summary>
        /// содержимое формы без файла
        /// </summary>
        public byte[] ContentMinusFiles
        {
            get { return m_formContent.ToArray(); }
        }

        /// <summary>
        /// Статус
        /// </summary>
        public string StatusKey
        {
            get { return m_statusKey; }
        }


        /// <summary>
        /// идентификатор
        /// </summary>
        public string Identifier
        {
            get { return m_id.ToString(); }
        }

        public string ProjectName { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Новый стрим для загрузки
        /// </summary>
        /// <param name="processor">Процессор, обрабатывающий файл</param>
        /// <param name="boundary">Какой строкой разделены части в запросе</param>
        /// <param name="encoding">Кодировка потока</param>
        public FormStream(IFileProcessor processor, string boundary, Encoding encoding)
        {
            m_defaultProcessor = processor;
            m_processor = processor;
            m_formContent = new MemoryStream();
            m_encoding = encoding;
            m_headerNeeded = true;
            m_position = 0;
            m_buffer = null;
            m_inField = false;
            m_inField = false;
            m_keepBackLength = boundary.Length + 6;
            m_fileError = false;


            BOUNDARY = m_encoding.GetBytes(boundary);
            EOF = m_encoding.GetBytes(boundary + "--\r\n");
            EOH = m_encoding.GetBytes("\r\n\r\n");
            CRLF = m_encoding.GetBytes("\r\n");
            ID_TAG = m_encoding.GetBytes(FormConsts.UPLOAD_ID_TAG);
            DEFAULT_PARAMS_TAG = m_encoding.GetBytes(FormConsts.UPLOAD_DEFAULT_PARAMETER_TAG);
            PARAMS_TAG = m_encoding.GetBytes(FormConsts.UPLOAD_PARAMETER_TAG);
            END_TAG = m_encoding.GetBytes(FormConsts.UPLOAD_END_TAG);
        }

        #endregion

        #region Stream implementation

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            if (m_buffer != null && m_buffer.Length > 0)
            {
                m_formContent.Write(m_buffer, 0, m_buffer.Length);
            }
        }

        public override long Length
        {
            get { return m_position; }
        }


        public override long Position
        {
            get { return m_position; }
            set { throw new NotImplementedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }


        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        private bool m_headerNeeded;
        private bool m_inField;
        private bool m_inFile;
        private byte[] m_buffer;

        /// <summary>
        /// Запись в поток. 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] bytes, int offset, int count)
        {
            byte[] input;
            int start = 0;
            int end = 0;

            if (m_buffer != null)
            {
                input = new byte[m_buffer.Length + count];
                Buffer.BlockCopy(m_buffer, 0, input, 0, m_buffer.Length);
                Buffer.BlockCopy(bytes, offset, input, m_buffer.Length, count);
            }
            else
            {
                input = new byte[count];
                Buffer.BlockCopy(bytes, offset, input, 0, count);
            }

            m_position += count;

            while (true)
            {
                if (m_headerNeeded)
                {
                    int prevStart = start;

                    start = IndexOf(input, BOUNDARY, start);

                    if (start >= 0)
                    {
                        end = IndexOf(input, EOF, start);

                        if (end == start)
                        {
                            WriteBytes(false, input, start, input.Length - start);
                            break;
                        }

                        // весь ли заголовок?
                        end = IndexOf(input, EOH, start);

                        if (end >= 0)
                        {
                            // заголовок полностью
                            m_inField = true;
                            m_headerNeeded = false;

                            Dictionary<string, string> headerItems = ParseHeader(input, start);

                            if (headerItems == null)
                            {
                                throw new Exception("Malformed header");
                            }

                            if (headerItems.ContainsKey("filename") && headerItems.ContainsKey("Content-Type"))
                            {
                                string fn = headerItems["filename"].Trim('"').Trim();

                                if (!String.IsNullOrEmpty(fn))
                                {
                                    try
                                    {
                                        m_fileName = headerItems["filename"].Trim('"');
                                        m_inFile = true;
                                        m_id = m_processor.StartNewFile(fn, headerItems["Content-Type"], headerItems);

                                        OnFileStarted(fn, m_id);
                                    }
                                    catch (Exception ex)
                                    {
                                        m_fileError = true;
                                        OnError(ex);
                                    }
                                }
                            }
                            else
                            {
                                m_inFile = false;
                                m_currentField = new MemoryStream();
                            }

                            start = end + 4;
                        }
                        else
                        {
                            m_buffer = new byte[input.Length - start];
                            Buffer.BlockCopy(input, start, m_buffer, 0, input.Length - start);
                            break;
                        }
                    }
                    else
                    {
                        m_buffer = new byte[input.Length - prevStart];
                        Buffer.BlockCopy(input, prevStart, m_buffer, 0, input.Length - prevStart);
                        break;
                    }
                }

                SectionResult res = null;

                if (m_inField)
                {
                    m_buffer = null; // Reset

                    // Process 
                    res = ProcessField(input, start);

                    if (res.NextAction == SectionResult.SectionAction.BoundaryReached)
                    {
                        m_headerNeeded = true;
                        m_inField = false;
                        start = res.NextOffset;

                        if (m_inFile)
                        {
                            m_inFile = false;

                            try
                            {
                                m_processor.EndFile();
                            }
                            catch (Exception ex)
                            {
                                OnError(ex);
                                m_fileError = true;
                            }
                            finally
                            {
                                if (m_fileError)
                                    OnFileCompletedError(m_fileName, m_processor.GetIdentifier(), m_ex);
                                else
                                    OnFileCompleted(m_fileName, m_processor.GetIdentifier());
                            }
                        }
                    }
                    else if (res.NextAction == SectionResult.SectionAction.NoBoundaryKeepBuffer)
                    {
                        m_buffer = new byte[input.Length - res.NextOffset];
                        Buffer.BlockCopy(input, res.NextOffset, m_buffer, 0, input.Length - res.NextOffset);
                        break;
                    }
                }

                if (!m_headerNeeded && !m_inField)
                {
                    throw new Exception("Прислана не форма с файлом");
                }
            }

        }

        #endregion

        #region Methods

        /// <summary>
        /// парсим заголовок
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private Dictionary<string, string> ParseHeader(byte[] bytes, int pos)
        {
            Dictionary<string, string> items;
            string header;
            string[] headerLines;
            int start;
            int end;

            string input = m_encoding.GetString(bytes, pos, bytes.Length - pos);

            start = input.IndexOf("\r\n", 0);
            if (start < 0) return null;

            end = input.IndexOf("\r\n\r\n", start);
            if (end < 0) return null;

            WriteBytes(false, bytes, pos, end + 4 - 0); 

            header = input.Substring(start, end - start);

            items = new Dictionary<string, string>();

            headerLines = Regex.Split(header, "\r\n");

            foreach (string hl in headerLines)
            {
                string[] lineParts = hl.Split(';');

                for (int i = 0; i < lineParts.Length; i++)
                {
                    string[] p;

                    if (i == 0)
                        p = lineParts[i].Split(':');
                    else
                        p = lineParts[i].Split('=');

                    if (p.Length == 2)
                    {
                        items.Add(p[0].Trim(), p[1].Trim());
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// ищем action в заголовке
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="bytes"></param>
        /// <param name="result"></param>
        /// <param name="boundaryPos"></param>
        /// <returns></returns>
        private bool TryParseActionField(byte[] tag, byte[] bytes, out string result, int boundaryPos)
        {
            int length = boundaryPos == -1 ? bytes.Length : boundaryPos;
            int startPos = IndexOf(bytes, tag, 0, length);

            if (startPos == 0)
            {
                byte[] idBytes;

                idBytes = new byte[length - 2];
                Buffer.BlockCopy(bytes, 0, idBytes, 0, idBytes.Length);
                result = m_encoding.GetString(idBytes);

                return true;
            }
            else
            {
                result = String.Empty;
                return false;
            }
        }

        /// <summary>
        /// парсим смотри что за данные в полях
        /// </summary>
        private void CheckForActionFields()
        {
            byte[] bytes = m_currentField.ToArray();
            string statusKey = string.Empty;
            int boundaryPos = IndexOf(bytes, BOUNDARY);

            // Status ID 
            if (!TryParseActionField(ID_TAG, bytes, out statusKey, boundaryPos))
            {
                string field = String.Empty;

                if (TryParseActionField(END_TAG, bytes, out field, boundaryPos))
                {
                    m_processor = m_defaultProcessor;
                    m_currentField = new MemoryStream();
                }
            }
            else
            {
                m_statusKey = statusKey;
                m_currentField = new MemoryStream();
            }
        }

       /// <summary>
       /// обрабатываем поле запроса
       /// </summary>
       /// <param name="bytes"></param>
       /// <param name="pos"></param>
       /// <returns></returns>
        private SectionResult ProcessField(byte[] bytes, int pos)
        {
            int end = -1;

            if (pos < bytes.Length - 1)
            {
                end = IndexOf(bytes, BOUNDARY, pos + 1);

                // Пропуск 2-х последних байтов поля
                if (end != -1 && m_inFile) end -= 2;
            }

            if (end >= 0)
            {
                WriteBytes(m_inFile, bytes, pos, end - pos);

                if (!m_inFile)
                {
                    CheckForActionFields();
                }

                return new SectionResult(end, SectionResult.SectionAction.BoundaryReached);
            }
            else
            {
                end = bytes.Length - m_keepBackLength;

                if (end > pos)
                {
                    WriteBytes(m_inFile, bytes, pos, end - pos);
                }
                else
                {
                    end = pos;
                }

                return new SectionResult(end, SectionResult.SectionAction.NoBoundaryKeepBuffer);
            }
        }

        /// <summary>
        /// запись данных в буфер
        /// </summary>
        /// <param name="toFile"></param>
        /// <param name="bytes"></param>
        /// <param name="pos"></param>
        /// <param name="count"></param>
        private void WriteBytes(bool toFile, byte[] bytes, int pos, int count)
        {
            if (toFile)
            {
                if (!m_fileError)
                {
                    try
                    {
                        m_processor.Write(bytes, pos, count);
                    }
                    catch (Exception ex)
                    {
                        m_fileError = true;
                        OnError(ex);
                    }
                }
            }
            else
            {
                m_fileError = false;
                m_formContent.Write(bytes, pos, count);
                if (m_currentField != null) m_currentField.Write(bytes, pos, count);
            }
        }

        /// <summary>
        /// позиция внутри байт-эррэй другого эррэя
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="checkFor"></param>
        /// <returns></returns>
        private int IndexOf(byte[] buffer, byte[] checkFor)
        {
            return IndexOf(buffer, checkFor, 0, buffer.Length);
        }

        /// <summary>
        /// позиция внутри байт-эррэй другого эррэя
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="checkFor"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        private int IndexOf(byte[] buffer, byte[] checkFor, int start)
        {
            return IndexOf(buffer, checkFor, start, buffer.Length - start);
        }

        /// <summary>
        /// позиция внутри байт-эррэй другого эррэя
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="checkFor"></param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private int IndexOf(byte[] buffer, byte[] checkFor, int start, int count)
        {
            int index = 0;

            int startPos = Array.IndexOf(buffer, checkFor[0], start);

            if (startPos != -1)
            {
                while ((startPos + index) < buffer.Length)
                {
                    if (buffer[startPos + index] == checkFor[index])
                    {
                        index++;
                        if (index == checkFor.Length)
                        {
                            return startPos;
                        }
                    }
                    else
                    {
                        startPos = Array.IndexOf(buffer, checkFor[0], startPos + index);
                        if (startPos == -1)
                        {
                            return -1;
                        }
                        index = 0;
                    }
                }
            }

            return -1;
        }

        #endregion

        #region IDisposable Members
        void IDisposable.Dispose()
        {
            if (m_processor != null)
            {
                m_processor.Dispose();
            }

            Flush();
            Close();
        }

        #endregion
    }
}
