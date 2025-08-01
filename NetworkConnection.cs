using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Net;
using Renci.SshNet;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GetStatistics
{
    /// <summary>
    /// Класс для временного подключения к сетевым ресурсам с использованием указанных учетных данных.
    /// Реализует IDisposable для автоматического отключения при выходе из блока using.
    /// </summary>
    /// 


    public class NetworkConnection 
    {
        public async Task<List<string>> ConnectViaSsh(ServerConfig server, SshClient _sshClient)
        {
            var logFiles = new List<string>();

            try
            {
                if (_sshClient != null && _sshClient.IsConnected)
                {
                    _sshClient.Disconnect();
                    _sshClient.Dispose();
                }

                _sshClient = new SshClient(server.Host, server.Username, server.Password);
                await Task.Run(() => _sshClient.Connect());

                if (_sshClient.IsConnected)
                {
                    // Получаем файлы из server.Path
                    var command = _sshClient.CreateCommand($"ls {server.Path} | grep -E '\\.log$|\\.txt$|\\.usrlog$'");
                    var result = await Task.Run(() => command.Execute());

                    if (command.ExitStatus == 0)
                    {
                        logFiles = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(file => Path.Combine(server.Path, file))
                                        .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка SSH: {ex.Message}");
            }

            return logFiles;
        }

        public async Task<List<string>> ConnectToLocal(ServerConfig server)
        {
            try
            {
                var patterns = new[] { "*.log", "*.usrlog", "*.txt" };
                return patterns.SelectMany(p => Directory.GetFiles(server.Path, p))
                              .Distinct()
                              .ToList();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Ошибка: {ex.Message}");
                return new List<string>(); // Возвращаем пустой список при ошибке
            }
        }

        public async Task<List<string>> ConnectViaSharedFolder(ServerConfig server)
        {
            try
            {
                string uncPath = $@"\\{server.Host}\{server.Path.Replace(":", "$")}";
                Debug.WriteLine(uncPath);

                var patterns = new[] { "*.log", "*.usrlog", "*.txt" };
                return patterns.SelectMany(p => Directory.GetFiles(uncPath, p))
                              .Distinct()
                              .ToList();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Ошибка: {ex.Message}");
                return new List<string>(); // Возвращаем пустой список при ошибке
            }
        }
    }
}