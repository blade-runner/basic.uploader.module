using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using ProtectionServer.Classes.Helper;

namespace Uploader
{
	/// <summary>
	/// Контроллер для загрузки
	/// добавляет необходимые поля в разметку
	/// поля передаются при постинге на сервер
	/// </summary>
	public class JUploadController : WebControl
	{
		#region Member variables



		HiddenField m_uploadID;
		UploadStatus m_status;
		bool m_enableManualProcessing = true;
		IFileProcessor _processor;


		#endregion

		#region Properties
		/// <summary>
		/// Устанавливаем процессор файлов по умолчанию
		/// </summary>
		public IFileProcessor DefaultFileProcessor {
			get { return _processor; }
			set {
				_processor = value as IFileProcessor;

				if (_processor == null) {
					throw new ArgumentException("Нужен класс поддерживающий интерфейс процессора");
				}
			}
		}


		/// <summary>
		/// Статус загрузки
		/// </summary>
		public UploadStatus Status {
			get { return m_status; }
			internal set { m_status = value; }
		}




        /// <summary>
        /// если нужно отключить модуль загрузки - ставим признак его недоступности и обрабатываем как обычно
        /// </summary>
		public bool EnableManualProcessing {
			get { return m_enableManualProcessing; }
			set { m_enableManualProcessing = value; }
		}
		#endregion

		#region .ctor
		public JUploadController() {
		}
		#endregion

	
		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);

			if (Page.IsPostBack && !UploadManager.Instance.ModuleInstalled && EnableManualProcessing) {
				ManualProcessUploads(); // сохраняем как обычно
			}

			EnsureChildControls();

			m_uploadID.Value = FormConsts.UPLOAD_ID_TAG + Guid.NewGuid().ToString();

		}



		/// <summary>
        /// модуль не установлен или отключен
		/// </summary>
		/// <param name="cc"></param>
		/// <param name="defaultProcessor"></param>
		/// <param name="status"></param>
		void ProcessUploadControls(ControlCollection cc, IFileProcessor defaultProcessor, UploadStatus status) {
			foreach (Control c in cc) {
				FileUpload fu = c as FileUpload;

				if (fu != null && fu.HasFile) {
					IFileProcessor processor = defaultProcessor;

					try {
						processor.StartNewFile(fu.FileName, fu.PostedFile.ContentType, null);
						processor.Write(fu.FileBytes, 0, fu.FileBytes.Length);
						processor.EndFile();

						status.UploadedFiles.Add(new UploadedFile(fu.FileName, processor.GetIdentifier(), null));
					} catch (Exception ex) {
						status.ErrorFiles.Add(new UploadedFile(fu.FileName, processor.GetIdentifier(), null, ex));
					}
				}

				if (c.HasControls()) {
					ProcessUploadControls(c.Controls, defaultProcessor, status);
				}
			}
		}

		/// <summary>
		/// модуль не установлен или отключен
		/// </summary>
		void ManualProcessUploads() {
		    IFileProcessor processor = _processor ?? UploadManager.Instance.GetProcessor();

			m_status = new UploadStatus(-1);

			if (processor != null) {
				ProcessUploadControls(Page.Controls, processor, m_status);
			}
		}

		/// <summary>
		/// добавляем обертку для разметки здесь
		/// </summary>
		protected override void CreateChildControls() {
			base.CreateChildControls();

		    m_uploadID = new HiddenField {ID = "uplid"};
		    Controls.Add(m_uploadID);

			//progress div
			Controls.Add(new LiteralControl("<div id=\"upProgressWrapper\"></div>"));

		}

	
	}
}
