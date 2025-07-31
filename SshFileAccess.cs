using Renci.SshNet;
using System;
using System.IO;
using System.Windows;

namespace GetStatistics
{
    public class SshFileAccess : IDisposable
    {
        private readonly SshClient _sshClient;
        private bool _disposed = false;

        public SshFileAccess(SshClient sshClient)
        {
            _sshClient = sshClient ?? throw new ArgumentNullException(nameof(sshClient), "SSH клиент не может быть null");
        }

        public string ReadRemoteFile(string filePath)
        {
            if (_disposed)
                throw new ObjectDisposedException("SshFileAccess", "Объект уже был освобожден");

            if (!_sshClient.IsConnected)
            {
                MessageBox.Show("SSH соединение не активно. Пожалуйста, подключитесь сначала.");
                return null;
            }

            try
            {
                using (var sftp = new SftpClient(_sshClient.ConnectionInfo))
                {
                    sftp.Connect();

                    using (var memoryStream = new MemoryStream())
                    {
                        sftp.DownloadFile(filePath, memoryStream);
                        memoryStream.Position = 0;
                        using (var reader = new StreamReader(memoryStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SFTP ошибка: {ex.Message}\nПробуем через SSH shell...");
                return ReadViaSshShell(filePath);
            }
        }

        private string ReadViaSshShell(string filePath)
        {
            try
            {
                var command = _sshClient.CreateCommand($"cat '{EscapeSshPath(filePath)}'");
                command.CommandTimeout = TimeSpan.FromSeconds(30);

                var result = command.Execute();

                if (command.ExitStatus != 0)
                    throw new IOException($"SSH команда завершилась с кодом {command.ExitStatus}: {command.Error}");

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SSH shell ошибка: {ex.Message}");
                return null;
            }
        }

        private string EscapeSshPath(string path)
        {
            return path?.Replace("'", "'\\''") ?? string.Empty;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}