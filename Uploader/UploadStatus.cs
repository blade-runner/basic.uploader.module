using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using ProtectionServer.Classes;

namespace Uploader
{
    /// <summary>
    /// Здесь информация по статусам текущей закачки.
    /// Статус сериализуется в json и отдается хэндлеру запроса статуса
    /// </summary>
    [DataContract]
    public class UploadStatus
    {
        #region Declarations

        public static UploadStatus EMPTY_STATUS = new UploadStatus();

        #endregion

        #region Properties

        [DataMember(Name = "Status", EmitDefaultValue = false)]
        public string EmptyStatus { get; private set; }


        public List<UploadedFile> UploadedFiles { get; internal set; }
        public List<UploadedFile> ErrorFiles { get; internal set; }

        // процент загрузки
        [DataMember(Name="prc", EmitDefaultValue = false)]
        public int ProgressPercent { get; private set; }


        public string CurrentFile { get; private set; }
        public object CurrentFileIdentifier { get; private set; } // идентификатор

        // всего файл
        [DataMember(Name = "total", EmitDefaultValue = false)]
        public long TotalSize { get; private set; }

        // переданно в данный момент
        [DataMember(Name = "current", EmitDefaultValue = false)]
        public long BytesSoFar { get; private set; }

        // todo: можно передавать время загрузки и прошедшее время
        public double TimeInSeconds { get; private set; }
        public DateTime StartTime { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// пустой статус
        /// </summary>
        public UploadStatus()
        {
            EmptyStatus = "empty";
        }

        /// <summary>
        /// обычный статус
        /// </summary>
        /// <param name="requestSize"></param>
        public UploadStatus(long requestSize)
        {
            UploadedFiles = new List<UploadedFile>();
            ErrorFiles = new List<UploadedFile>();
            ProgressPercent = 0;
            CurrentFile = String.Empty;
            BytesSoFar = 0;
            TotalSize = 0;
            TimeInSeconds = 0;
            StartTime = DateTime.Now;
            TotalSize = requestSize;
        }

        public UploadStatus(bool overload)
        {
            EmptyStatus = "overloaded";
        }

        #endregion

        #region Methods

        /// <summary>
        /// текущий файл, апдейт
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        public void UpdateFile(string fileName, object identifier)
        {
            CurrentFile = fileName;
            CurrentFileIdentifier = identifier;
        }


        /// <summary>
        /// апдейт прогресса загрузки
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="fileName"></param>
        /// <param name="identifier"></param>
        public void UpdateBytes(long bytes, string fileName, object identifier)
        {
            CurrentFile = fileName;
            CurrentFileIdentifier = identifier;
            BytesSoFar = bytes;
            ProgressPercent = 100 - (int)(100 * (((double)TotalSize - (double)BytesSoFar) / (double)TotalSize));
            if (ProgressPercent < 0) ProgressPercent = 0;
            if (ProgressPercent > 100) ProgressPercent = 100;
            TimeSpan time = DateTime.Now - StartTime;
            TimeInSeconds = time.TotalSeconds;
        }




        #endregion


    }
}
