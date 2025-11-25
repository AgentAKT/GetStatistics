using GetStatistics;
using GetStatistics.Models;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

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

                // Добавить эту строку для отображения
                ApplyLogFilters(content, _logRichTextBox, true, _mainWindow.IsCalculatorMode());
            }
            else
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Файл не найден: {filePath}");
                }

                long fileSize = (await GetFileSize(filePath, server, sshClient) / 1000000);
                if (fileSize > 300) // 300MB
                {
                    if (!HasAtLeastOneFilter(_getLeftFilters()))
                    {
                        MessageBox.Show($"Размер {fileSize} Мб! \nВидит Бог я такого не вынесу!\n Добавь какой-нибудь фильтр");
                        await ReadLocalFileLineByLineWithFiltering(filePath);
                    }

                }
                else
                { 
                    content = await ReadLocalFile(filePath);
                    ApplyLogFilters(content, _logRichTextBox, true, _mainWindow.IsCalculatorMode());
                }
            }

        }
        catch (Exception ex)
        {
            _statusText.Text = $"Ошибка: {ex.Message}";
            throw; // Перебрасываем исключение для обработки в UI
        }
    }


    


    private bool HasAtLeastOneFilter(FilterParameters filters)
    {
        return !string.IsNullOrEmpty(filters.Filter_One) ||
               !string.IsNullOrEmpty(filters.Filter_Two) ||
               !string.IsNullOrEmpty(filters.SearchText_One) ||
               !string.IsNullOrEmpty(filters.SearchText_Two);
    }

    private async Task<long> GetFileSize(string filePath, ServerConfig server, SshClient sshClient = null)
    {
        try
        {
            if (server.Protocol == "SSH" && sshClient != null && sshClient.IsConnected)
            {
                // Для SSH-соединения используем команду stat
                var command = sshClient.CreateCommand($"stat -c%s '{filePath}'");
                var result = await Task.Run(() => command.Execute());

                if (command.ExitStatus == 0 && long.TryParse(result, out long fileSize))
                {
                    return fileSize;
                }
                throw new Exception($"Не удалось получить размер файла: {command.Error}");
            }
            else
            {
                // Для локального файла
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException("Файл не найден", filePath);
                }
                return fileInfo.Length;
            }
        }
        catch (Exception ex)
        {
            _mainWindow.StatusText.Text = $"Ошибка при получении размера файла: {ex.Message}";
            throw;
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

            var result = new StringBuilder();
            int lineCount = 0;
            int matchedLines = 0;
            const int maxDisplayedLines = 10000;
            bool limitReached = false;

            using (var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 65536,
                FileOptions.SequentialScan | FileOptions.Asynchronous))
            using (var streamReader = new StreamReader(fileStream))
            {
                string line;
                while ((line = await streamReader.ReadLineAsync()) != null)
                {
                    lineCount++;

                    // Периодически обновляем статус
                    if (lineCount % 1000 == 0)
                    {
                        _mainWindow.StatusText.Text = $"Обработано строк: {lineCount} | Найдено: {matchedLines}";
                        await Task.Yield();
                    }

                    // Проверяем соответствие строки фильтрам
                    var leftFilters = _getLeftFilters();
                    var activeFilters = GetActiveFilters(leftFilters);
                    
                    if (activeFilters.Count > 0 && activeFilters.All(filter => line.Contains(filter)))
                    {
                        matchedLines++;

                        // Добавляем строку в RichTextBox только если не превышен лимит
                        if (matchedLines <= maxDisplayedLines)
                        {
                            _logRichTextBox.Dispatcher.Invoke(() =>
                            {
                                var paragraph = new Paragraph();
                                paragraph.Inlines.Add(new Run(line + "\n"));
                                _logRichTextBox.Document.Blocks.Clear();
                                _logRichTextBox.Document.Blocks.Add(paragraph);
                            });
                        }
                        else if (!limitReached)
                        {
                            limitReached = true;
                            // Добавляем сообщение о превышении лимита
                            _logRichTextBox.Dispatcher.Invoke(() =>
                            {
                                var paragraph = new Paragraph();
                                paragraph.Inlines.Add(new Run(
                                    $"\n\n--- ПРЕДУПРЕЖДЕНИЕ ---\n" +
                                    $"Отображено только первые {maxDisplayedLines} строк из {matchedLines} найденных.\n" +
                                    $"Файл слишком большой. Используйте более конкретные фильтры для уточнения результатов.\n" +
                                    $"------------------------\n\n"));
                                _logRichTextBox.Document.Blocks.Clear();
                                _logRichTextBox.Document.Blocks.Add(paragraph);
                            });
                        }
                    }
                }
            }

            // Показываем финальное уведомление если превышен лимит
            if (limitReached)
            {
                _logRichTextBox.Dispatcher.Invoke(() =>
                {
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run(
                        $"\n--- ОБРАБОТКА ЗАВЕРШЕНА ---\n" +
                        $"Всего обработано строк: {lineCount:N0}\n" +
                        $"Найдено соответствий: {matchedLines:N0}\n" +
                        $"Отображено: {maxDisplayedLines:N0} строк\n" +
                        $"Используйте более точные фильтры для уменьшения результатов.\n" +
                        $"-----------------------------\n"));
                    _logRichTextBox.Document.Blocks.Clear();
                    _logRichTextBox.Document.Blocks.Add(paragraph);
                });

                // Показываем MessageBox с предупреждением
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Файл содержит слишком много соответствий!\n\n" +
                        $"Всего найдено: {matchedLines:N0} строк\n" +
                        $"Отображено: {maxDisplayedLines:N0} строк\n\n" +
                        $"Рекомендации:\n" +
                        $"• Используйте более конкретные фильтры\n" +
                        $"• Добавьте дополнительные условия поиска\n" +
                        $"• Уточните временной диапазон\n" +
                        $"• Используйте комбинацию фильтров",
                        "Большой объем данных",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }

            _mainWindow.StringCounter_Main.Content = matchedLines.ToString();
            _mainWindow.StatusText.Text = $"Готово. Обработано строк: {lineCount:N0} | Найдено: {matchedLines:N0}";
            return result.ToString();
        }
        catch (Exception ex)
        {
            _mainWindow.StatusText.Text = $"Ошибка: {ex.Message}";
            throw;
        }
        finally
        {
            _mainWindow.StatusProgressBar.Visibility = Visibility.Collapsed;
            _mainWindow.StatusProgressBar.IsIndeterminate = false;
        }
    }

    private async Task ReadLocalFileLineByLineWithFiltering(string filePath)
    {
        try
        {
            // Показываем индикатор загрузки
            _mainWindow.StatusProgressBar.Visibility = Visibility.Visible;
            _mainWindow.StatusProgressBar.IsIndeterminate = true;
            _mainWindow.StatusText.Text = "Чтение и фильтрация файла...";

            // Получаем левые фильтры
            var leftFilters = _getLeftFilters();
            var activeFilters = GetActiveFilters(leftFilters);

            // Очищаем RichTextBox перед началом
            _logRichTextBox.Document.Blocks.Clear();
            var paragraph = new Paragraph();
            _logRichTextBox.Document.Blocks.Add(paragraph);

            int lineCount = 0;
            int matchedLines = 0;
            const int maxDisplayedLines = 10000;
            bool limitReached = false;

            using (var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 65536,
                FileOptions.SequentialScan | FileOptions.Asynchronous))
            using (var streamReader = new StreamReader(fileStream))
            {
                string line;
                while ((line = await streamReader.ReadLineAsync()) != null)
                {
                    lineCount++;

                    // Периодически обновляем статус
                    if (lineCount % 1000 == 0)
                    {
                        _mainWindow.StatusText.Text = $"Обработано строк: {lineCount} | Найдено: {matchedLines}";
                        await Task.Yield();
                    }

                    // Проверяем соответствие строки фильтрам
                    if (activeFilters.Count > 0 && activeFilters.All(filter => line.Contains(filter)))
                    {
                        matchedLines++;

                        // Добавляем строку в RichTextBox только если не превышен лимит
                        if (matchedLines <= maxDisplayedLines)
                        {
                            _logRichTextBox.Dispatcher.Invoke(() =>
                            {
                                paragraph.Inlines.Add(new Run(line + "\n"));
                            });
                        }
                        else if (!limitReached)
                        {
                            limitReached = true;
                            // Добавляем сообщение о превышении лимита
                            _logRichTextBox.Dispatcher.Invoke(() =>
                            {
                                paragraph.Inlines.Add(new Run(
                                    $"\n\n--- ПРЕДУПРЕЖДЕНИЕ ---\n" +
                                    $"Отображено только первые {maxDisplayedLines} строк из {matchedLines} найденных.\n" +
                                    $"Файл слишком большой. Используйте более конкретные фильтры для уточнения результатов.\n" +
                                    $"------------------------\n\n"));
                            });
                        }
                    }
                }
            }

            // Показываем финальное уведомление если превышен лимит
            if (limitReached)
            {
                _logRichTextBox.Dispatcher.Invoke(() =>
                {
                    paragraph.Inlines.Add(new Run(
                        $"\n--- ОБРАБОТКА ЗАВЕРШЕНА ---\n" +
                        $"Всего обработано строк: {lineCount:N0}\n" +
                        $"Найдено соответствий: {matchedLines:N0}\n" +
                        $"Отображено: {maxDisplayedLines:N0} строк\n" +
                        $"Используйте более точные фильтры для уменьшения результатов.\n" +
                        $"-----------------------------\n"));
                });

                // Показываем MessageBox с предупреждением
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Файл содержит слишком много соответствий!\n\n" +
                        $"Всего найдено: {matchedLines:N0} строк\n" +
                        $"Отображено: {maxDisplayedLines:N0} строк\n\n" +
                        $"Рекомендации:\n" +
                        $"• Используйте более конкретные фильтры\n" +
                        $"• Добавьте дополнительные условия поиска\n" +
                        $"• Уточните временной диапазон\n" +
                        $"• Используйте комбинацию фильтров",
                        "Большой объем данных",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }

            _mainWindow.StringCounter_Main.Content = matchedLines.ToString();
            _mainWindow.StatusText.Text = $"Готово. Обработано строк: {lineCount:N0} | Найдено: {matchedLines:N0}";
        }
        catch (Exception ex)
        {
            _mainWindow.StatusText.Text = $"Ошибка: {ex.Message}";
            throw;
        }
        finally
        {
            _mainWindow.StatusProgressBar.Visibility = Visibility.Collapsed;
            _mainWindow.StatusProgressBar.IsIndeterminate = false;
        }
    }

    private List<string> GetActiveFilters(FilterParameters filters)
    {
        var activeFilters = new List<string>();

        if (!string.IsNullOrEmpty(filters.Filter_One))
            activeFilters.Add(filters.Filter_One);
        if (!string.IsNullOrEmpty(filters.Filter_Two))
            activeFilters.Add(filters.Filter_Two);
        if (!string.IsNullOrEmpty(filters.SearchText_One))
            activeFilters.Add(filters.SearchText_One);
        if (!string.IsNullOrEmpty(filters.SearchText_Two))
            activeFilters.Add(filters.SearchText_Two);

        return activeFilters;
    }

    private async Task<string> ReadSshFile(string filePath)
    {
        try
        {
            // Показываем индикатор загрузки
            _mainWindow.StatusProgressBar.Visibility = Visibility.Visible;
            _mainWindow.StatusProgressBar.IsIndeterminate = true; // Бесконечная анимация
            _mainWindow.StatusText.Text = "Чтение файла через SSH...";

            var result = new StringBuilder();
            int lineCount = 0;
            int matchedLines = 0;
            const int maxDisplayedLines = 10000;
            bool limitReached = false;

            // Создаем новую команду для каждого запроса
            var command = _mainWindow._sshClient.CreateCommand($"cat '{filePath}'");
            command.CommandTimeout = TimeSpan.FromSeconds(30);

            Console.WriteLine($"Executing SSH command: {command.CommandText}");
            var resultText = command.Execute();
            Console.WriteLine($"Command executed, exit status: {command.ExitStatus}");

            if (command.ExitStatus != 0)
            {
                throw new Exception($"SSH command failed (code {command.ExitStatus}): {command.Error}");
            }

            // Обрабатываем результат построчно
            var lines = resultText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                lineCount++;

                // Периодически обновляем статус
                if (lineCount % 1000 == 0)
                {
                    _mainWindow.StatusText.Text = $"Обработано строк: {lineCount} | Найдено: {matchedLines}";
                    await Task.Yield();
                }

                // Проверяем соответствие строки фильтрам
                var leftFilters = _getLeftFilters();
                var activeFilters = GetActiveFilters(leftFilters);
                
                if (activeFilters.Count > 0 && activeFilters.All(filter => line.Contains(filter)))
                {
                    matchedLines++;

                    // Добавляем строку в RichTextBox только если не превышен лимит
                    if (matchedLines <= maxDisplayedLines)
                    {
                        _logRichTextBox.Dispatcher.Invoke(() =>
                        {
                            var paragraph = new Paragraph();
                            paragraph.Inlines.Add(new Run(line + "\n"));
                            _logRichTextBox.Document.Blocks.Clear();
                            _logRichTextBox.Document.Blocks.Add(paragraph);
                        });
                    }
                    else if (!limitReached)
                    {
                        limitReached = true;
                        // Добавляем сообщение о превышении лимита
                        _logRichTextBox.Dispatcher.Invoke(() =>
                        {
                            var paragraph = new Paragraph();
                            paragraph.Inlines.Add(new Run(
                                $"\n\n--- ПРЕДУПРЕЖДЕНИЕ ---\n" +
                                $"Отображено только первые {maxDisplayedLines} строк из {matchedLines} найденных.\n" +
                                $"Файл слишком большой. Используйте более конкретные фильтры для уточнения результатов.\n" +
                                $"------------------------\n\n"));
                            _logRichTextBox.Document.Blocks.Clear();
                            _logRichTextBox.Document.Blocks.Add(paragraph);
                        });
                    }
                }
            }

            // Показываем финальное уведомление если превышен лимит
            if (limitReached)
            {
                _logRichTextBox.Dispatcher.Invoke(() =>
                {
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run(
                        $"\n--- ОБРАБОТКА ЗАВЕРШЕНА ---\n" +
                        $"Всего обработано строк: {lineCount:N0}\n" +
                        $"Найдено соответствий: {matchedLines:N0}\n" +
                        $"Отображено: {maxDisplayedLines:N0} строк\n" +
                        $"Используйте более точные фильтры для уменьшения результатов.\n" +
                        $"-----------------------------\n"));
                    _logRichTextBox.Document.Blocks.Clear();
                    _logRichTextBox.Document.Blocks.Add(paragraph);
                });

                // Показываем MessageBox с предупреждением
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Файл содержит слишком много соответствий!\n\n" +
                        $"Всего найдено: {matchedLines:N0} строк\n" +
                        $"Отображено: {maxDisplayedLines:N0} строк\n\n" +
                        $"Рекомендации:\n" +
                        $"• Используйте более конкретные фильтры\n" +
                        $"• Добавьте дополнительные условия поиска\n" +
                        $"• Уточните временной диапазон\n" +
                        $"• Используйте комбинацию фильтров",
                        "Большой объем данных",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }

            _mainWindow.StringCounter_Main.Content = matchedLines.ToString();
            _mainWindow.StatusText.Text = $"Готово. Обработано строк: {lineCount:N0} | Найдено: {matchedLines:N0}";
            return result.ToString();
        }
        catch (Exception ex)
        {
            _mainWindow.StatusText.Text = $"Ошибка: {ex.Message}";
            throw;
        }
        finally
        {
            _mainWindow.StatusProgressBar.Visibility = Visibility.Collapsed;
            _mainWindow.StatusProgressBar.IsIndeterminate = false;
        }
    }

    private void ClearAndApplyFilters(string content)
    {
        _logRichTextBox.Dispatcher.Invoke(() =>
        {
            _logRichTextBox.Document.Blocks.Clear();
            ApplyLogFilters(content, _logRichTextBox, true, _mainWindow.IsCalculatorMode());
        });
    }

    public void ApplyLogFilters(string content, RichTextBox richTextBox, bool isLeftFilter, bool isCalculatorMode)
    {
        var filterLogFile = new FilterLogFile(richTextBox, _mainWindow);
        filterLogFile.SetContent(content);

        if (isCalculatorMode)
        {
            var leftFilters = _getLeftFilters();
            var rightFilters = _getRightFilters();
            filterLogFile.ApplyLogFilters(leftFilters, rightFilters);
        }
        else
        {
            var filters = isLeftFilter ? _getLeftFilters() : _getRightFilters();
            filterLogFile.ApplyLogFilters(filters, isLeftFilter);
        }
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