using GetStatistics;
using GetStatistics.Models;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
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

    public async Task LoadLogFile(string filePath, ServerConfig server, SshClient sshClient = null)
    {
        try
        {
            string content;

            if (server.Protocol == "SSH" && sshClient != null && sshClient.IsConnected)
            {
                var command = sshClient.CreateCommand($"cat '{filePath}'");
                content = await Task.Run(() => command.Execute());

                if (command.ExitStatus != 0)
                {
                    throw new Exception($"SSH error: {command.Error}");
                }
            }
            else
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Файл не найден: {filePath}");
                }

                content = await ReadLocalFile(filePath);
            }

            ApplyLogFilters(content, _logRichTextBox, true);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Ошибка: {ex.Message}";
            throw; // Перебрасываем исключение для обработки в UI
        }
    }

    private async Task<string> ReadLocalFile(string filePath)
    {
        try
        {
            // Показываем индикатор загрузки
            _mainWindow.StatusProgressBar.Visibility = Visibility.Visible;
            _mainWindow.StatusProgressBar.IsIndeterminate = true; // Бесконечная анимация
            _mainWindow.StatusText.Text = "Чтение файла...";

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
        finally
        {
            // Скрываем индикатор после завершения (успешного или с ошибкой)
            _mainWindow.StatusProgressBar.Visibility = Visibility.Collapsed;
            _mainWindow.StatusProgressBar.IsIndeterminate = false;
            _mainWindow.StatusText.Text = "Готово";
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

    public bool MatchesFilter(string line, FilterParameters filterParams)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        // Проверяем соответствие всем заданным фильтрам (которые не пустые)
        bool matches = true;

        // Проверка первого условия фильтра (если задано)
        if (!string.IsNullOrEmpty(filterParams.SearchText_One))
        {
            matches = line.Contains(filterParams.SearchText_One);
        }

        // Проверка второго условия фильтра (если задано)
        if (matches && !string.IsNullOrEmpty(filterParams.SearchText_Two))
        {
            matches = line.Contains(filterParams.SearchText_Two);
        }

        // Проверка первого комбобокса (если задан)
        if (matches && !string.IsNullOrEmpty(filterParams.Filter_One))
        {
            matches = line.Contains(filterParams.Filter_One);
        }

        // Проверка второго комбобокса (если задан)
        if (matches && !string.IsNullOrEmpty(filterParams.Filter_Two))
        {
            matches = line.Contains(filterParams.Filter_Two);
        }

        return matches;
    }
}