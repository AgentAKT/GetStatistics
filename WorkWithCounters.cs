using GetStatistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace LogNavigator
{
    internal class WorkWithCounters
    {
        private readonly MainWindow _mainWindow;

        public WorkWithCounters(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void ClearLeftCounter()
        {
            _mainWindow.StringCounter_Left.Content = "";
        }

        public void ClearMainCounter()
        {
            _mainWindow.StringCounter_Main.Content = "";
        }

        public void SetLeftCounter(int value)
        {
            _mainWindow.StringCounter_Left.Content = $"Найдено: {value}";
        }

        public void SetMainCounter(int value)
        {
            _mainWindow.StringCounter_Main.Content = $"Файлов: {value}";
        }
    }
}

