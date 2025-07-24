using GetStatistics;
using GetStatistics.Models;
using Renci.SshNet;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;

public class LogFileService
{
    private readonly RichTextBox _logRichTextBox;
    private readonly TextBlock _statusText;
    private readonly Func<FilterParameters> _getLeftFilters;
    private readonly Func<FilterParameters> _getRightFilters;
    private readonly MainWindow _mainWindow;

    public LogFileService(
        RichTextBox logRichTextBox,
        TextBlock statusText,
        Func<FilterParameters> getLeftFilters,
        Func<FilterParameters> getRightFilters,
        MainWindow mainWindow)
    {
        _logRichTextBox = logRichTextBox;
        _statusText = statusText;
        _getLeftFilters = getLeftFilters;
        _getRightFilters = getRightFilters;
        _mainWindow = mainWindow;
    }

    public async Task LoadLogFile(string filePath, ServerConfig server)
    {
        try
        {
            string content;

            if (server.Protocol == "Local")
            {
                content = await ReadLocalFile(filePath);
                _statusText.Text = "Локальный файл загружен";
            }
            else if (server.Protocol == "SSH")
            {
                if (_mainWindow._sshClient == null)
                {
                    _statusText.Text = "SSH-клиент не инициализирован";
                    Console.WriteLine("SSH-клиент не инициализирован");
                    return;
                }

                if (!_mainWindow._sshClient.IsConnected)
                {
                    _statusText.Text = "SSH-соединение не активно";
                    return;
                }

                content = await ReadSshFile(filePath);
                _statusText.Text = "Файл прочитан по SSH";
            }
            else
            {
                content = await ReadLocalFile(filePath);
                _statusText.Text = "Папка открыта локально";
            }

            ClearAndApplyFilters(content);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Ошибка: {ex.Message}";
            Console.WriteLine($"Ошибка в LoadLogFile: {ex}");
        }
    }

    private async Task<string> ReadLocalFile(string filePath)
    {
        using (var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite))
        using (var streamReader = new StreamReader(fileStream))
        {
            return await streamReader.ReadToEndAsync();
        }
    }

    private async Task<string> ReadSshFile(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Создаем новую команду для каждого запроса
                var command = _mainWindow._sshClient.CreateCommand($"cat '{filePath}'");
                command.CommandTimeout = TimeSpan.FromSeconds(30);

                Console.WriteLine($"Executing SSH command: {command.CommandText}");
                var result = command.Execute();
                Console.WriteLine($"Command executed, exit status: {command.ExitStatus}");

                if (command.ExitStatus != 0)
                {
                    throw new Exception($"SSH command failed (code {command.ExitStatus}): {command.Error}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SSH command error: {ex}");
                _statusText.Dispatcher.Invoke(() =>
                {
                    _statusText.Text = $"SSH Error: {ex.Message}";
                });
                throw;
            }
        });
    }

    private void ClearAndApplyFilters(string content)
    {
        _logRichTextBox.Dispatcher.Invoke(() =>
        {
            _logRichTextBox.Document.Blocks.Clear();
            ApplyLogFilters(content, _logRichTextBox, true);
        });
    }

    public void ApplyLogFilters(string content, RichTextBox richTextBox, bool isLeftFilter)
    {
        var filterLogFile = new FilterLogFile(richTextBox, _mainWindow);
        filterLogFile.SetContent(content);
        
        var filters = isLeftFilter ? _getLeftFilters() : _getRightFilters();
        filterLogFile.ApplyLogFilters(filters, isLeftFilter);
    }

}