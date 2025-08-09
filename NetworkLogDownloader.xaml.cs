using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace NetworkLogDownloader
{
    public partial class MainWindow : Window
    {
        private List<string> _sharedFolders = new List<string>();
        private string _localDownloadPath = "";

        public MainWindow()
        {
            InitializeComponent();

            LoadConfig();
            SharedFoldersList.ItemsSource = _sharedFolders;
        }

        private void LoadConfig()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _sharedFolders = config.GetSection("SharedFolders").Get<List<string>>();
            _localDownloadPath = config.GetValue<string>("LocalDownloadPath");

            if (!Directory.Exists(_localDownloadPath))
                Directory.CreateDirectory(_localDownloadPath);
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFolder = SharedFoldersList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedFolder))
            {
                MessageBox.Show("Выберите папку для загрузки.");
                return;
            }

            DownloadButton.IsEnabled = false;
            DownloadProgressBar.Value = 0;

            try
            {
                await Task.Run(() => CopyFolder(selectedFolder, _localDownloadPath, UpdateProgress));
                MessageBox.Show("Загрузка завершена.");

                // Обновить список локальных логов
                LocalLogsList.ItemsSource = Directory.GetDirectories(_localDownloadPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке: {ex.Message}");
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                DownloadProgressBar.Value = 0;
            }
        }

        private void CopyFolder(string sourcePath, string destRoot, Action<double> progressCallback)
        {
            var folderName = new DirectoryInfo(sourcePath).Name;
            var destPath = Path.Combine(destRoot, folderName);

            CopyAll(new DirectoryInfo(sourcePath), new DirectoryInfo(destPath), progressCallback);
        }

        private void CopyAll(DirectoryInfo source, DirectoryInfo target, Action<double> progressCallback)
        {
            if (!target.Exists)
                target.Create();

            var files = source.GetFiles("*", SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int copiedFiles = 0;

            foreach (var file in files)
            {
                var relativePath = file.FullName.Substring(source.FullName.Length + 1);
                var targetFilePath = Path.Combine(target.FullName, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                file.CopyTo(targetFilePath, true);

                copiedFiles++;
                progressCallback?.Invoke((double)copiedFiles / totalFiles * 100);
            }
        }

        private void UpdateProgress(double progressValue)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = progressValue;
            });
        }
    }
}
