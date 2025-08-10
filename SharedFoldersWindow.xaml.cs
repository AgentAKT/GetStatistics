using LogNavigator.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using static Org.BouncyCastle.Math.EC.ECCurve;
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
                    // Создаем дефолтный конфиг
                    var defaultConfig = new Config { SharedFolders = new List<SharedFolder>() };

                    // Создаем директорию, если не существует
                    var configDir = Path.GetDirectoryName(_configPath);
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    // Сохраняем конфиг
                    File.WriteAllText(_configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));

                    // Для отладки можно добавить:
                    Console.WriteLine($"Создан новый конфиг по пути: {_configPath}");
                    return true;
                }

                // Если файл существует - загружаем его
                var json = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);

                // Проверяем, что десериализация прошла успешно
                if (config == null)
                {
                    MessageBox.Show("Ошибка: конфигурационный файл поврежден");
                    return false;
                }
                FoldersListView.ItemsSource = config.SharedFolders;
                // Для отладки можно вывести содержимое
                Console.WriteLine($"Успешно загружен конфиг: {json}");
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
                var config = new { SharedFolders = SharedFolders.ToList() };
                // Раскомментируйте и используйте Newtonsoft.Json:
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения конфигурации: {ex.Message}");
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            string path = ServerTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // Здесь реализация подключения к шаре
                    BrowseNetworkShare(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка подключения: {ex.Message}");
                }
            }
        }

        private void BrowseNetworkShare(string path)
        {
            FilesTreeView.Items.Clear();
            var root = new FileSystemItem { Name = Path.GetFileName(path), Path = path };
            LoadDirectory(root);
            FilesTreeView.Items.Add(root);
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

        private void DownloadFile_Click(object sender, RoutedEventArgs e)
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
                    File.Copy(item.Path, saveDialog.FileName, true);
                    MessageBox.Show("Файл успешно скачан!");
                }
            }
        }

        private void DownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FilesTreeView.SelectedItem is FileSystemItem item && item.IsDirectory)
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string destination = folderDialog.SelectedPath;
                    try
                    {
                        // Здесь реализация копирования папки
                        CopyDirectory(item.Path, Path.Combine(destination, item.Name));
                        MessageBox.Show("Папка успешно скачана!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите папку для скачивания");
            }
        }

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