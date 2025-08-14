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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GetStatistics
{
    public partial class SharedFoldersWindow : Window, INotifyPropertyChanged
    {
        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GetStatistics", "Configs", "SharedFoldersConfig.json");

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isBatchDownloadRunning = false;
        private ServerGroup _selectedGroup;

        public ObservableCollection<ServerGroup> ServerGroups { get; } = new ObservableCollection<ServerGroup>();

        public ServerGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _selectedGroup = value;
                OnPropertyChanged();
                // Update the ServerTextBox when group changes
                if (_selectedGroup != null && _selectedGroup.SharedFolders.Any())
                {
                    ServerTextBox.Text = _selectedGroup.SharedFolders.First().SharePath;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public SharedFoldersWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadConfig();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    var defaultConfig = new Config { ServerGroups = new List<ServerGroup>() };
                    SaveConfig(defaultConfig);
                    return true;
                }

                var json = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);

                if (config == null)
                {
                    MessageBox.Show("Ошибка: конфигурационный файл поврежден");
                    return false;
                }

                ServerGroups.Clear();
                foreach (var group in config.ServerGroups)
                {
                    var serverGroup = new ServerGroup(group.SharedFolders)
                    {
                        GroupName = group.GroupName
                    };
                    ServerGroups.Add(serverGroup);
                }


                GroupsComboBox.ItemsSource = ServerGroups;
                if (ServerGroups.Any())
                {
                    SelectedGroup = ServerGroups.First();
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки конфигурации: {ex.Message}");
                return false;
            }
        }

        private void SaveConfig(Config config = null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                var configToSave = config ?? new Config
                {
                    ServerGroups = ServerGroups.Select(group => new ServerGroup
                    {
                        GroupName = group.GroupName,
                        SharedFolders = new ObservableCollection<SharedFolder>(group.SharedFolders)
                    }).ToList()
                };

                var json = JsonConvert.SerializeObject(configToSave, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения конфигурации: {ex.Message}");
            }
        }

         
        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Введите название группы:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                var newGroup = new ServerGroup
                {
                    GroupName = dialog.InputText,
                    SharedFolders = new ObservableCollection<SharedFolder>()
                };

                ServerGroups.Add(newGroup);
                SelectedGroup = newGroup; 
                SaveConfig(); 
            }
        }

        private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGroup != null)
            {
                if (MessageBox.Show($"Удалить группу '{SelectedGroup.GroupName}'?",
                    "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    ServerGroups.Remove(SelectedGroup);
                    if (ServerGroups.Any())
                    {
                        SelectedGroup = ServerGroups.First();
                    }
                    else
                    {
                        SelectedGroup = null;
                    }
                    SaveConfig();
                }
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

            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку для сохранения файлов",
                ShowNewFolderButton = true,
                // Устанавливаем начальный путь из настроек
                SelectedPath = !string.IsNullOrEmpty(Properties.Settings.Default.LastOpenedFolder) &&
                              Directory.Exists(Properties.Settings.Default.LastOpenedFolder)
                    ? Properties.Settings.Default.LastOpenedFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string destination = folderDialog.SelectedPath;

                // Сохраняем выбранный путь в настройки
                Properties.Settings.Default.LastOpenedFolder = destination;
                Properties.Settings.Default.Save();

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
                            Dispatcher.Invoke(() => LoadingText.Text =
                                $"Скачивание файлов ({processedFiles}/{totalFiles})...\n" +
                                $"{file.Name}\n" +
                                $"Пропущено: {skippedFiles}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error copying {file.Path}: {ex.Message}");
                        }
                    }

                    string message = _cancellationTokenSource.Token.IsCancellationRequested
                        ? $"Операция прервана пользователем. Скачано {processedFiles} из {totalFiles} файлов."
                        : $"Скачивание завершено! Успешно скачано {processedFiles} из {totalFiles} файлов.";

                    MessageBox.Show(message);
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

            if (SelectedGroup == null || !SelectedGroup.SharedFolders.Any())
            {
                MessageBox.Show("В выбранной группе нет серверов для скачивания");
                return;
            }

            // Запрос подтверждения перед началом скачивания
            var confirmationResult = MessageBox.Show(
                "Будут скачаны все логи со всех серверов выбранной группы. Процесс может занять значительное время.\n\nПродолжить?",
                "Подтверждение скачивания",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (confirmationResult != MessageBoxResult.OK)
            {
                return; // Пользователь отменил операцию
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

                int totalServers = SelectedGroup.SharedFolders.Count;
                int processedServers = 0;

                foreach (var server in SelectedGroup.SharedFolders)
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

                                if (File.Exists(destPath))
                                {
                                    var sourceFileInfo = new FileInfo(file);
                                    var destFileInfo = new FileInfo(destPath);

                                    if (sourceFileInfo.Length == destFileInfo.Length &&
                                        sourceFileInfo.LastWriteTime == destFileInfo.LastWriteTime)
                                    {
                                        processedFiles++;
                                        continue;
                                    }
                                }

                                await Task.Run(() => File.Copy(file, destPath, false));
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
            if (SelectedGroup == null) return;

            var dialog = new SharedFolderEditDialog();
            if (dialog.ShowDialog() == true)
            {
                SelectedGroup.SharedFolders.Add(dialog.Folder);
                SaveConfig();
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(FoldersListView.SelectedItem is SharedFolder folder)) return;

            SelectedGroup.SharedFolders.Remove(folder);
            SaveConfig();
        }

        private void EditFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!(FoldersListView.SelectedItem is SharedFolder selectedFolder)) return;

            var dialog = new SharedFolderEditDialog(selectedFolder);
            if (dialog.ShowDialog() == true)
            {
                int index = SelectedGroup.SharedFolders.IndexOf(selectedFolder);
                SelectedGroup.SharedFolders[index] = dialog.Folder;
                SaveConfig();
            }
        }


        

        private void DuplicateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersListView.SelectedItem is SharedFolder selectedFolder)
            {
                var newFolder = new SharedFolder
                {
                    ServerName = $"{selectedFolder.ServerName} (копия)",
                    SharePath = selectedFolder.SharePath
                };

                SelectedGroup.SharedFolders.Add(newFolder);
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

        private void GroupsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        //private void EditFolder_Click(object sender, RoutedEventArgs e)
        //{
        //    if (FoldersListView.SelectedItem is SharedFolder selectedFolder)
        //    {
        //        var dialog = new SharedFolderEditDialog(selectedFolder);
        //        if (dialog.ShowDialog() == true)
        //        {
        //            int index = SharedFolders.IndexOf(selectedFolder);
        //            SharedFolders[index] = dialog.Folder;
        //            SaveConfig();
        //        }
        //    }
        //}

        //private void DuplicateFolder_Click(object sender, RoutedEventArgs e)
        //{
        //    if (FoldersListView.SelectedItem is SharedFolder selectedFolder)
        //    {
        //        var newFolder = new SharedFolder
        //        {
        //            ServerName = $"{selectedFolder.ServerName} (копия)",
        //            SharePath = selectedFolder.SharePath
        //        };

        //        SharedFolders.Add(newFolder);
        //        SaveConfig();
        //    }
        //}
    }

    // Модели данных
    public class Config
    {
        public List<ServerGroup> ServerGroups { get; set; } = new List<ServerGroup>();
    }

    public class ServerGroup
    {
        public string GroupName { get; set; }
        public ObservableCollection<SharedFolder> SharedFolders { get; set; }

        public ServerGroup()
        {
            SharedFolders = new ObservableCollection<SharedFolder>();
        }

        public ServerGroup(IEnumerable<SharedFolder> folders)
        {
            SharedFolders = new ObservableCollection<SharedFolder>(folders);
        }
    }

    public class SharedFolder
    {
        public string ServerName { get; set; }
        public string SharePath { get; set; }
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