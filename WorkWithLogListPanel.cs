using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LogNavigator
{

    public class WorkWithLogListPanel
    {
        private readonly ListBox _logList;
        private readonly TextBlock _statusText;
        private List<string> _logFiles;
        private string _currentLogFilePath;

        public WorkWithLogListPanel(ListBox logList, TextBlock statusText)
        {
            _logList = logList ?? throw new ArgumentNullException(nameof(logList));
            _statusText = statusText ?? throw new ArgumentNullException(nameof(_statusText));
            _logFiles = new List<string>();
            _currentLogFilePath = string.Empty;
        }
        public void UpdateLogList(List<string> filePaths)
        {
            try
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    // Сохраняем полные пути
                    _logFiles = filePaths;

                    // Отображаем только имена файлов
                    _logList.ItemsSource = filePaths.Select(Path.GetFileName).ToList();

                    _statusText.Text = $"Найдено {filePaths.Count} файлов";

                    // Сбрасываем текущий выбранный файл
                    _currentLogFilePath = string.Empty;
                    _logList.UnselectAll();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                    _statusText.Text = $"Ошибка обновления списка: {ex.Message}");
            }
        }

        public List<string> LogFiles => _logFiles;
        public string CurrentLogFilePath => _currentLogFilePath;

    }
}
