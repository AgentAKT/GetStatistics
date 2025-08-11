using LogNavigator.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Threading;

namespace GetStatistics
{
    public partial class SharedFoldersWindow : Window
    {
        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GetStatistics", "Configs", "SharedFoldersConfig.json");

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isBatchDownloadRunning = false;

        public ObservableCollection<SharedFolder> SharedFolders { get; } = new ObservableCollection<SharedFolder>();

        public SharedFoldersWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadConfig();
            Console.WriteLine(_configPath);
        }

        private bool LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    var defaultConfig = new Config { SharedFolders = new List<SharedFolder>() };

                    var configDir = Path.GetDirectoryName(_configPath);
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    File.WriteAllText(_configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                    return true;
                }

                var json = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);

                if (config == null)
                {
                    MessageBox.Show("Ошибка: конфигурационный файл поврежден");
                    return false;
                }

                SharedFolders.Clear();
                foreach (var folder in config.SharedFolders)
                {
                    SharedFolders.Add(folder);
                }

                FoldersListView.ItemsSource = SharedFolders;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки конфигурации: {ex.Message}");
                return false;
            }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                var config = new Config { SharedFolders = SharedFolders.ToList() };
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения конфигурации: {ex.Message}");
            }
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            string path = ServerTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    ShowLoader("Подключение к папке...");
                    await Task.Run(() => BrowseNetworkShare(path));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка подключения: {ex.Message}");
                }
                finally
                {
                    HideLoader();
                }
            }
        }

        private async void DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (FilesTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = item.Name,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        ShowLoader("Скачивание файла...");
                        await Task.Run(() => File.Copy(item.Path, saveDialog.FileName, true));
                        MessageBox.Show("Файл успешно скачан!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка скачивания: {ex.Message}");
                    }
                    finally
                    {
                        HideLoader();
                    }
                }
            }
        }

        private async void DownloadAllFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchDownloadRunning)
            {
                MessageBox.Show("Операция скачивания уже выполняется");
                return;
            }

            int skippedFiles = 0;

            var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string destination = folderDialog.SelectedPath;

                try
                {
                    _isBatchDownloadRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    CancelDownloadButton.IsEnabled = true;
                    DownloadAllButton.IsEnabled = false;

                    // Get all files from the tree
                    var allFiles = GetAllFilesFromTree(FilesTreeView.Items);
                    int totalFiles = allFiles.Count;
                    int processedFiles = 0;

                    ShowLoader($"Скачивание файлов (0/{totalFiles})...");

                    foreach (var file in allFiles)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Dispatcher.Invoke(() => LoadingText.Text = "Отмена операции...");
                            break;
                        }


                        string destPath = Path.Combine(destination, file.Name);
                        string destDir = Path.GetDirectoryName(destPath);

                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        try
                        {
                            if (File.Exists(destPath))
                            {

                                var sourceFile = new FileInfo(file.Path);
                                var destFile = new FileInfo(destPath);

                                if (sourceFile.Length == destFile.Length && 
                                    sourceFile.LastWriteTime == destFile.LastWriteTime)
                                {
                                    skippedFiles++;
                                    continue;
                                }
                                
                            }

                            await Task.Run(() => File.Copy(file.Path, destPath, true));
                            processedFiles++;
                            Dispatcher.Invoke(() => LoadingText.Text = $"Скачивание файлов ({processedFiles}/{totalFiles})...\n{file.Name}\n Пропущено: {skippedFiles}");
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with other files
                            Console.WriteLine($"Error copying {file.Path}: {ex.Message}");
                        }
                    }

                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        MessageBox.Show($"Скачивание завершено! Успешно скачано {processedFiles} из {totalFiles} файлов.");
                    }
                    else
                    {
                        MessageBox.Show($"Операция прервана пользователем. Скачано {processedFiles} из {totalFiles} файлов.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при скачивании файлов: {ex.Message}");
                }
                finally
                {
                    _isBatchDownloadRunning = false;
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                    CancelDownloadButton.IsEnabled = false;
                    DownloadAllButton.IsEnabled = true;
                    HideLoader();
                }
            }
        }

        private List<FileSystemItem> GetAllFilesFromTree(ItemCollection items)
        {
            var files = new List<FileSystemItem>();

            foreach (FileSystemItem item in items)
            {
                if (item.IsDirectory)
                {
                    files.AddRange(GetAllFilesFromDirectory(item));
                }
                else
                {
                    files.Add(item);
                }
            }

            return files;
        }

        private List<FileSystemItem> GetAllFilesFromDirectory(FileSystemItem directory)
        {
            var files = new List<FileSystemItem>();

            foreach (var child in directory.Children)
            {
                if (child.IsDirectory)
                {
                    files.AddRange(GetAllFilesFromDirectory(child));
                }
                else
                {
                    files.Add(child);
                }
            }

            return files;
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchDownloadRunning && _cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                CancelDownloadButton.IsEnabled = false;
            }
        }

        private void FoldersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is SharedFolder selectedFolder)
            {
                ServerTextBox.Text = selectedFolder.SharePath;
                Connect_Click(null, null);
            }
        }

        private async void AllDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchDownloadRunning)
            {
                MessageBox.Show("Операция скачивания уже выполняется");
                return;
            }

            if (!SharedFolders.Any())
            {
                MessageBox.Show("Нет добавленных серверов для скачивания");
                return;
            }

            try
            {
                _isBatchDownloadRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                CancelDownloadButton.IsEnabled = true;
                DownloadAllButton.IsEnabled = false;

                // Создаем папку на рабочем столе
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string folderName = $"{DateTime.Now:yyyyMMdd}_logs";
                string mainFolderPath = Path.Combine(desktopPath, folderName);

                if (!Directory.Exists(mainFolderPath))
                {
                    Directory.CreateDirectory(mainFolderPath);
                }

                ShowLoader($"Начало скачивания...");

                int totalServers = SharedFolders.Count;
                int processedServers = 0;

                foreach (var server in SharedFolders)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Dispatcher.Invoke(() => LoadingText.Text = "Отмена операции...");
                        break;
                    }

                    processedServers++;
                    Dispatcher.Invoke(() => LoadingText.Text = $"Обработка сервера {server.ServerName} ({processedServers}/{totalServers})...");

                    try
                    {
                        // Создаем подпапку для сервера
                        string serverFolderPath = Path.Combine(mainFolderPath, server.ServerName);
                        if (!Directory.Exists(serverFolderPath))
                        {
                            Directory.CreateDirectory(serverFolderPath);
                        }

                        // Получаем все файлы из корневой папки сервера
                        var files = await Task.Run(() =>
                        {
                            try
                            {
                                return Directory.GetFiles(server.SharePath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка доступа к серверу {server.ServerName}: {ex.Message}");
                                return Array.Empty<string>();
                            }
                        });

                        int totalFiles = files.Length;
                        int processedFiles = 0;

                        foreach (var file in files)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                                break;

                            try
                            {
                                string destPath = Path.Combine(serverFolderPath, Path.GetFileName(file));

                                // Проверяем, существует ли файл
                                if (File.Exists(destPath))
                                {
                                    // Сравниваем размер и дату изменения файлов
                                    var sourceFileInfo = new FileInfo(file);
                                    var destFileInfo = new FileInfo(destPath);

                                    // Если файлы идентичны (по размеру и дате изменения) - пропускаем
                                    if (sourceFileInfo.Length == destFileInfo.Length &&
                                        sourceFileInfo.LastWriteTime == destFileInfo.LastWriteTime)
                                    {
                                        processedFiles++;
                                        continue;
                                    }

                                    //// Если файлы разные, можно добавить суффикс (опционально)
                                    //string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                                    //string extension = Path.GetExtension(file);
                                    //int counter = 1;
                                    //string newDestPath;
                                    //do
                                    //{
                                    //    newDestPath = Path.Combine(serverFolderPath,
                                    //        $"{fileNameWithoutExt}_{counter}{extension}");
                                    //    counter++;
                                    //} while (File.Exists(newDestPath));

                                    //destPath = newDestPath;
                                }

                                await Task.Run(() => File.Copy(file, destPath, false)); // overwrite = false
                                processedFiles++;
                                Dispatcher.Invoke(() => LoadingText.Text =
                                    $"Сервер {server.ServerName}: скачано {processedFiles}/{totalFiles} файлов");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка копирования файла {file}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка обработки сервера {server.ServerName}: {ex.Message}");
                    }
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    MessageBox.Show($"Скачивание завершено! Файлы сохранены в папку {mainFolderPath}");
                }
                else
                {
                    MessageBox.Show($"Операция прервана пользователем. Часть файлов сохранена в {mainFolderPath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скачивании файлов: {ex.Message}");
            }
            finally
            {
                _isBatchDownloadRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                CancelDownloadButton.IsEnabled = false;
                DownloadAllButton.IsEnabled = true;
                HideLoader();
            }
        }

        private void BrowseNetworkShare(string path)
        {
            Dispatcher.Invoke(() =>
            {
                FilesTreeView.Items.Clear();
                var root = new FileSystemItem { Name = Path.GetFileName(path), Path = path, IsDirectory = true };
                LoadDirectory(root);
                FilesTreeView.Items.Add(root);
            });
        }

        private void LoadDirectory(FileSystemItem item)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(item.Path))
                {
                    var child = new FileSystemItem { Name = Path.GetFileName(dir), Path = dir, IsDirectory = true };
                    item.Children.Add(child);
                    LoadDirectory(child);
                }

                foreach (var file in Directory.GetFiles(item.Path))
                {
                    item.Children.Add(new FileSystemItem
                    {
                        Name = Path.GetFileName(file),
                        Path = file,
                        Icon = "/Icons/file.png"
                    });
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SharedFolderEditDialog();
            if (dialog.ShowDialog() == true)
            {
                SharedFolders.Add(dialog.Folder);
                SaveConfig();
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersListView.SelectedItem is SharedFolder folder)
            {
                SharedFolders.Remove(folder);
                SaveConfig();
            }
        }

        private void FoldersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoldersListView.SelectedItem == null) return;

            if (FoldersListView.SelectedItem is SharedFolder selectedFolder)
            {
                ServerTextBox.Text = $@"{selectedFolder.SharePath}";
            }
        }

        private void ShowLoader(string message = "Подключение...")
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = message;
                OverlayGrid.Visibility = Visibility.Visible;
            });
        }

        private void HideLoader()
        {
            Dispatcher.Invoke(() =>
            {
                OverlayGrid.Visibility = Visibility.Collapsed;
            });
        }
    }

    public class Config
    {
        public List<SharedFolder> SharedFolders { get; set; } = new List<SharedFolder>();
    }

    public class FileSystemItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Icon { get; set; }
        public bool IsDirectory { get; set; }
        public ObservableCollection<FileSystemItem> Children { get; } = new ObservableCollection<FileSystemItem>();
    }
}