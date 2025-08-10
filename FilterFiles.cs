using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace GetStatistics
{
    internal class FilterFiles
    {
        private MainWindow _mainWindow;

        public FilterFiles(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public async Task<List<string>> FilterByToday(List<string> files, string protocol = "Local")
        {
            // Получаем текущую дату во всех возможных форматах
            var todayFormats = new[]
            {
        DateTime.Today.ToString("yyyyMMdd"),
        DateTime.Today.ToString("ddMMyyyy"),
        DateTime.Today.ToString("MMddyyyy"),
        DateTime.Today.ToString("yyyy-MM-dd"),
        DateTime.Today.ToString("dd-MM-yyyy")
    };

            try
            {
                List<string> filteredFiles;

                if (protocol == "SSH")
                {
                    // Фильтрация по имени файла для SSH
                    filteredFiles = files
                        .Where(file => todayFormats.Any(format =>
                            Path.GetFileName(file).Contains(format)))
                        .ToList();
                }
                else
                {
                    // Фильтрация по дате изменения для локальных файлов
                    filteredFiles = files
                        .Where(file =>
                        {
                            try
                            {
                                return File.GetLastWriteTime(file).Date == DateTime.Today;
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .ToList();
                }

                // Обновление статуса в UI
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.StatusText.Text = $"Найдено файлов за сегодня: {filteredFiles.Count}";
                    Console.WriteLine($"Успешно отфильтровано {filteredFiles.Count} файлов");
                });

                return filteredFiles;
            }
            catch (Exception ex)
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.StatusText.Text = "Ошибка фильтрации файлов";
                    Console.WriteLine($"Ошибка фильтрации: {ex.Message}");
                });
                return new List<string>();
            }
        }

        public async Task<List<string>> FilterFilesByName(List<string> filteredFiles, string _searchText)
        {
            filteredFiles = filteredFiles
                    .Where(file => Path.GetFileName(file).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            //StatusText.Text = $"Логи отсортированы по фильтру {_searchText}";
            return filteredFiles;
        }
    }
}
