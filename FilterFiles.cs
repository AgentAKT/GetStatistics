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

        public async Task<List<string>> FilterByToday(List<string> files)
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            Console.WriteLine(today);

            var filtered = files.Where(file => Path.GetFileName(file).Contains(today)).ToList();

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.StatusText.Text = $"Логи отсортированы по дате {today}";
            });

            return filtered;
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
