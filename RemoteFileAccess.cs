using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class RemoteFileAccess
{
    /// <summary>
    /// Подключается к удаленному серверу и читает содержимое файла
    /// </summary>
    /// <param name="serverAddress">IP или имя сервера</param>
    /// <param name="filePath">Путь к файлу на сервере (например: C:\Logs\app.log)</param>
    /// <param name="username">Логин</param>
    /// <param name="password">Пароль</param>
    /// <returns>Содержимое файла или null при ошибке</returns>
    public string ReadRemoteFile(string serverAddress, string filePath, string username, string password)
    {
        string uncPath = ConvertToUncPath(serverAddress, filePath);

        try
        {
            using (new NetworkConnection(uncPath, new NetworkCredential(username, password)))
            {
                return ReadPossiblyLockedFile(uncPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка доступа к файлу: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Читает файл, даже если он занят другим процессом
    /// </summary>
    private string ReadPossiblyLockedFile(string filePath)
    {
        try
        {
            // Пытаемся прочитать с разрешением на совместный доступ
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                return reader.ReadToEnd();
            }
        }
        catch (IOException ex)
        {
            // Если не удалось, пробуем сделать временную копию
            try
            {
                return TryReadViaTempCopy(filePath);
            }
            catch
            {
                throw new IOException($"Не удалось прочитать файл {filePath}. Ошибка: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Альтернативный метод чтения через временную копию
    /// </summary>
    private string TryReadViaTempCopy(string sourcePath)
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.Copy(sourcePath, tempFile, overwrite: true);
            return File.ReadAllText(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Преобразует обычный путь в UNC-формат
    /// </summary>
    private string ConvertToUncPath(string server, string path)
    {
        // Убираем двоеточие (C: -> C$)
        string drive = path.Split(':')[0] + "$";
        string relativePath = path.Substring(path.IndexOf(':') + 1).Replace('\\', '/');
        return $@"\\{server}\{drive}{relativePath}";
    }

    /// <summary>
    /// Класс для временного подключения к сетевым ресурсам
    /// </summary>
    private class NetworkConnection : IDisposable
    {
        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(
            ref NETRESOURCE lpNetResource,
            string lpPassword,
            string lpUsername,
            int dwFlags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(
            string lpName,
            int dwFlags,
            bool fForce);

        private readonly string _networkPath;

        public NetworkConnection(string networkPath, NetworkCredential credentials)
        {
            _networkPath = networkPath;

            var netResource = new NETRESOURCE
            {
                dwType = 1, // RESOURCETYPE_DISK
                lpRemoteName = networkPath
            };

            int result = WNetAddConnection2(
                ref netResource,
                credentials.Password,
                credentials.UserName,
                0); // CONNECT_TEMPORARY

            if (result != 0)
                throw new Win32Exception(result, "Ошибка подключения к сетевому ресурсу");
        }

        public void Dispose()
        {
            WNetCancelConnection2(_networkPath, 0, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }
    }
}