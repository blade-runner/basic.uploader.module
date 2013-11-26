using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Web;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace Uploader
{
	/// <summary>
	/// �������� ��� ProcessorInit.
	/// </summary>
	public class FileProcessorInitEventArgs : EventArgs
	{
		#region Declarations

		IFileProcessor _processor;

		#endregion

		#region Properties

		public IFileProcessor Processor
		{
			get { return _processor; }
		}

		#endregion

		#region Constructor

		public FileProcessorInitEventArgs(IFileProcessor processor)
		{
			_processor = processor;
		}

		#endregion
	}

	/// <summary>
	/// ������� ��� ProcessorInit.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="args"></param>
	public delegate void FileProcessorInitEventHandler(object sender, FileProcessorInitEventArgs args);

	/// <summary>
	/// ����� ��������� ���������.
	/// ������� ����������� �����������.
	/// </summary>
	public sealed class UploadManager
	{
		#region Declarations

		static UploadManager _instance = null;
		static readonly object Padlock = new object();
		const int MIN_BUFFER_SIZE = 1024;
		const int DEF_BUFFER_SIZE = 1024 * 28;
		Type m_processorType;
		int m_bufferSize;





		#endregion

		#region Constructor

		UploadManager()
		{
			//todo: ���� ������ ������ � �������� �������
			m_processorType = typeof(FileSystemProcessor);
			m_bufferSize = DEF_BUFFER_SIZE;
		}

	
		#endregion

		#region Events

		public event FileProcessorInitEventHandler ProcessorInit;

		/// <summary>
		/// ProcessorInit event.
		/// </summary>
		/// <param name="processor"></param>
		public void OnProcessorInit(IFileProcessor processor)
		{
			if (ProcessorInit != null)
				ProcessorInit(this, new FileProcessorInitEventArgs(processor));
		}

		#endregion

		#region Properties


		internal void SetStatus(UploadStatus status, string key)
		{
			HttpContext.Current.Application[FormConsts.STATUS_KEY + key] = status;
		}

	    /// <summary>
	    /// HTTP ������ ����������?
	    /// </summary>
	    public bool ModuleInstalled { get; internal set; }

	    /// <summary>
		/// ������ ������� ������ ��������
		/// </summary>
		internal UploadStatus Status
		{
			get
			{
				string key = string.Empty;

                key = HttpContext.Current.Request.QueryString[FormConsts.STATUS_KEY];
                if (key == null)
                {
                    key = (string)HttpContext.Current.Items[FormConsts.STATUS_KEY];
                    if (key == null)
                        return null;
                }

                return HttpContext.Current.Application[FormConsts.STATUS_KEY + key] as UploadStatus;
			}
			set
			{
			    string key = HttpContext.Current.Request.QueryString[FormConsts.STATUS_KEY];
			    if (key != null)
				{
					SetStatus(value, key);
				}
			}
		}

		/// <summary>
		/// ������ ������ ��� ������ ������
		/// </summary>
		public int BufferSize
		{
			get { return m_bufferSize; }
			set
			{
				if (m_bufferSize <= MIN_BUFFER_SIZE)
					throw new ArgumentException("���������� ������� ������ ������");
				m_bufferSize = value;
			}
		}

		/// <summary>
		/// ������� ������� 
		/// thread safe
		/// </summary>
		public static UploadManager Instance
		{
			get
			{
				lock (Padlock)
				{
				    return _instance ?? (_instance = new UploadManager());
				}
			}
		}

		/// <summary>
		/// IFileProcessor
		/// </summary>
		public Type ProcessorType
		{
			get { return m_processorType; }
			set
			{
				if (value == null || value.GetInterface("IFileProcessor", false) == null)
					throw new ArgumentException("����� IFileProcessor");
				m_processorType = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Factory method ������ IFileProcessor.
		/// </summary>
		/// <returns>The created file processor.</returns>
		public IFileProcessor GetProcessor()
		{
		    var processor = (IFileProcessor)Activator.CreateInstance(m_processorType);
			OnProcessorInit(processor);
			return processor;
		}

	

		#endregion

	}
}
