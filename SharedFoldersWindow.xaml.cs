using LogNavigator.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using static Org.BouncyCastle.Math.EC.ECCurve;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Input;
//using Microsoft.Win32;
//using System.Text.Json;

namespace GetStatistics
{
    public partial class SharedFoldersWindow : Window
    {
        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GetStatistics", "Configs", "SharedFoldersConfig.json");

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

                // Очищаем текущую коллекцию и добавляем загруженные элементы
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

        private void FoldersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Проверяем, что щелчок был именно по элементу списка, а не по пустому месту
            if (e.OriginalSource is FrameworkElement element && element.DataContext is SharedFolder selectedFolder)
            {
                // Устанавливаем путь в текстовое поле
                ServerTextBox.Text = selectedFolder.SharePath;

                // Автоматически подключаемся (можно убрать, если нужно только заполнять поле)
                Connect_Click(null, null);
            }
        }

        private async void DownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FilesTreeView.SelectedItem is FileSystemItem item && item.IsDirectory)
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string destination = folderDialog.SelectedPath;
                    try
                    {
                        ShowLoader("Скачивание папки...");
                        await Task.Run(() => CopyDirectory(item.Path, Path.Combine(destination, item.Name)));
                        MessageBox.Show("Папка успешно скачана!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}");
                    }
                    finally
                    {
                        HideLoader();
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите папку для скачивания");
            }
        }

        private void BrowseNetworkShare(string path)
        {
            Dispatcher.Invoke(() =>
            {
                FilesTreeView.Items.Clear();
                var root = new FileSystemItem { Name = Path.GetFileName(path), Path = path };
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
                    LoadDirectory(child); // Рекурсивная загрузка
                }

                foreach (var file in Directory.GetFiles(item.Path))
                {
                    item.Children.Add(new FileSystemItem
                    {
                        Name = Path.GetFileName(file),
                        Path = file,
                        Icon = "/Icons/file.png" // Ваша иконка
                    });
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        //private void DownloadFile_Click(object sender, RoutedEventArgs e)
        //{
        //    if (FilesTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
        //    {
        //        var saveDialog = new SaveFileDialog
        //        {
        //            FileName = item.Name,
        //            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        //        };

        //        if (saveDialog.ShowDialog() == true)
        //        {
        //            File.Copy(item.Path, saveDialog.FileName, overwrite: true);
        //            MessageBox.Show("Файл успешно скачан!");
        //        }
        //    }
        //}

        

        // Метод для копирования директории
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