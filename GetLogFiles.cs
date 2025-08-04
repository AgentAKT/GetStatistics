using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogNavigator
{
    internal class GetLogFiles
    {
        public async Task<List<string>> GetLocalFilesAsync(string path)
        {
            try
            {
                var patterns = new[] { "*.log", "*.usrlog", "*.txt" };

                // Читаем файлы асинхронно через Task.Run
                var files = await Task.Run(() =>
                    patterns.SelectMany(p => Directory.GetFiles(path, p))
                            .Distinct()
                            .ToList());

                return files;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Ошибка при получении логов: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
