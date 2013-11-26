using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace Uploader
{
	/// <summary>
	/// интерфейс для работы с файлом
	/// форм стрим использует любой процессор, поддерживающий этот интерфейс, для сохранения файлов непосредственно в какое-либо хранилище 
	/// </summary>
	public interface IFileProcessor : IDisposable
	{
		/// <summary>
		/// Новое файло.
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="contentType"></param>
		/// <param name="headerItems"></param>
		/// <returns>возвращает id файла, для идентификации на клиенте</returns>
		object StartNewFile(string fileName, string contentType, Dictionary<string, string> headerItems);

		/// <summary>
		/// Запись в файл
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		void Write(byte[] buffer, int offset, int count);

		/// <summary>
		/// Завершение записи
		/// </summary>
		void EndFile();

        /// <summary>
        /// удаление файла при неполной загрузке
        /// </summary>
	    void DestroyFile();
		
        /// <summary>
		/// Что за файл грузим
		/// Если фала нет выдает null
		/// </summary>
		/// <returns></returns>
		string GetFileName();

		/// <summary>
		/// выдает id загружаемого файла
		/// </summary>
		/// <returns></returns>
		object GetIdentifier();

		/// <summary>
		/// Заголовки запроса
		/// </summary>
		Dictionary<string, string> GetHeaderItems();
	}
}
