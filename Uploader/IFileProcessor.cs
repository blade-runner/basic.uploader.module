using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace Uploader
{
	/// <summary>
	/// ��������� ��� ������ � ������
	/// ���� ����� ���������� ����� ���������, �������������� ���� ���������, ��� ���������� ������ ��������������� � �����-���� ��������� 
	/// </summary>
	public interface IFileProcessor : IDisposable
	{
		/// <summary>
		/// ����� �����.
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="contentType"></param>
		/// <param name="headerItems"></param>
		/// <returns>���������� id �����, ��� ������������� �� �������</returns>
		object StartNewFile(string fileName, string contentType, Dictionary<string, string> headerItems);

		/// <summary>
		/// ������ � ����
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		void Write(byte[] buffer, int offset, int count);

		/// <summary>
		/// ���������� ������
		/// </summary>
		void EndFile();

        /// <summary>
        /// �������� ����� ��� �������� ��������
        /// </summary>
	    void DestroyFile();
		
        /// <summary>
		/// ��� �� ���� ������
		/// ���� ���� ��� ������ null
		/// </summary>
		/// <returns></returns>
		string GetFileName();

		/// <summary>
		/// ������ id ������������ �����
		/// </summary>
		/// <returns></returns>
		object GetIdentifier();

		/// <summary>
		/// ��������� �������
		/// </summary>
		Dictionary<string, string> GetHeaderItems();
	}
}
