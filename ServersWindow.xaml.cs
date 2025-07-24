using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack;
using System.IO;
//using System.Windows.Forms;

namespace GetStatistics
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class ServersWindow : Window
    {
        private Config _config;
        private string _configPath = "config.json";
        private DateTime _lastTreeViewClickTime;
        private DateTime _lastPasswordBoxClickTime;
        private object _lastClickedItem;
        private readonly MainWindow _mainWindow;
        public ServersWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            Loaded += ServersWindow_Loaded;
        }

        private void ServersWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Загрузка конфигурации
            try
            {
                _config = Config.Load(_configPath);
                if (_config == null)
                {
                    _config = new Config { Folders = new List<ServerFolder>() };
                }
            }
            catch
            {
                _config = new Config { Folders = new List<ServerFolder>() };
            }

            // Заполнение TreeView
            LoadServersTree();

            // Заполнение ComboBox папок
            FolderComboBox.ItemsSource = _config.Folders;
            FolderComboBox.DisplayMemberPath = "Name";
        }

        private void LoadServersTree()
        {
            ServersTreeView.Items.Clear();

            foreach (var folder in _config.Folders)
            {
                var folderNode = new TreeViewItem
                {
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                {
                    new Image { Source = new BitmapImage(new Uri(folder.IconPath, UriKind.RelativeOrAbsolute)), Width = 16, Height = 16 },
                    new TextBlock { Text = folder.Name, Margin = new Thickness(5, 0, 0, 0) }
                }
                    },
                    Tag = folder // Сохраняем ссылку на папку
                };

                foreach (var server in folder.Servers)
                {
                    var serverNode = new TreeViewItem
                    {
                        Header = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                    {
                        new Image { Source = new BitmapImage(new Uri(server.IconPath, UriKind.RelativeOrAbsolute)), Width = 16, Height = 16 },
                        new TextBlock { Text = server.Name, Margin = new Thickness(5, 0, 0, 0) }
                    }
                        },
                        Tag = server // Сохраняем ссылку на сервер
                    };

                    folderNode.Items.Add(serverNode);
                }

                ServersTreeView.Items.Add(folderNode);
                folderNode.IsExpanded = true;
            }
        

            // Развернуть все узлы по умолчанию
            foreach (var item in ServersTreeView.Items)
            {
                if (item is TreeViewItem treeViewItem)
                {
                    treeViewItem.IsExpanded = true;
                }
            }
        }

        private void ServersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ServersTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                if (selectedItem.Tag is ServerFolder selectedFolder)
                {
                    // Выбрана папка
                    FolderComboBox.SelectedItem = selectedFolder;
                    ClearServerFields();
                }
                else if (selectedItem.Tag is ServerConfig selectedServer)
                {
                    // Выбран сервер
                    var parentFolder = FindParentFolder(selectedServer);
                    if (parentFolder != null)
                    {
                        FolderComboBox.SelectedItem = parentFolder;
                    }

                    // Заполняем поля данными сервера
                    NameTextBox.Text = selectedServer.Name;
                    HostTextBox.Text = selectedServer.Host;
                    ProtocolComboBox.SelectedItem = ProtocolComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Content.ToString() == selectedServer.Protocol);
                    UsernameTextBox.Text = selectedServer.Username;
                    PasswordBox.Password = selectedServer.Password;
                    PathTextBox.Text = selectedServer.Path;
                    PortTextBox.Text = selectedServer.Port?.ToString() ?? "";
                }
            }
        }

        private ServerFolder FindParentFolder(ServerConfig server)
        {
            return _config.Folders.FirstOrDefault(f => f.Servers.Contains(server));
        }

        private void ClearServerFields()
        {
            NameTextBox.Text = "";
            HostTextBox.Text = "";
            ProtocolComboBox.SelectedIndex = -1;
            UsernameTextBox.Text = "";
            PasswordBox.Password = "";
            PathTextBox.Text = "";
            PortTextBox.Text = "";
        }

        private void AddServer_Click(object sender, RoutedEventArgs e)
        {
            if (FolderComboBox.SelectedItem is ServerFolder selectedFolder)
            {
                var newServer = new ServerConfig
                {
                    Name = NameTextBox.Text,
                    Host = HostTextBox.Text,
                    Protocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content.ToString(),
                    Username = UsernameTextBox.Text,
                    Password = PasswordBox.Password,
                    Path = PathTextBox.Text,
                    Port = int.TryParse(PortTextBox.Text, out var port) ? port : (int?)null
                };

                selectedFolder.Servers.Add(newServer);
                LoadServersTree();
            }
        }

        private void RemoveServer_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is TreeViewItem selectedItem &&
                selectedItem.Tag is ServerConfig selectedServer)
            {
                var folder = FindParentFolder(selectedServer);
                folder?.Servers.Remove(selectedServer);
                LoadServersTree();
                ClearServerFields();
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            // Создаем текстовое поле для ввода
            var inputTextBox = new TextBox
            {
                MinWidth = 200,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Объявляем dialog до создания команды
            Window dialog = null;

            // Создаем команду для кнопки OK
            var okCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(inputTextBox.Text))
                {
                    _config.Folders.Add(new ServerFolder
                    {
                        Name = inputTextBox.Text.Trim(),
                        Servers = new List<ServerConfig>()
                    });
                    LoadServersTree();
                }
                dialog?.Close();
            });

            // Создаем диалоговое окно
            dialog = new Window
            {
                Title = "Новая папка",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Children =
            {
                new TextBlock { Text = "Введите имя новой папки:" },
                inputTextBox,
                new Button
                {
                    Content = "ОК",
                    Margin = new Thickness(0, 10, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Command = okCommand // Используем предварительно созданную команду
                }
            }
                }
            };

            dialog.ShowDialog();
        }

        public class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

            public void Execute(object parameter) => _execute();

            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Сохраняем конфигурацию
                _config.Save(_configPath);

                // Обновляем данные в главном окне
                if (_mainWindow != null)
                {
                    // 1. Перезагружаем конфиг
                    _mainWindow.LoadConfig();

                    // 2. Принудительно обновляем привязки
                    //_mainWindow.comboBoxFolders.ItemsSource = null;
                    //_mainWindow.comboBoxFolders.ItemsSource = _config.Folders;

                    // 3. Обновляем статус
                    _mainWindow.StatusText.Text = "Конфигурация обновлена";
                }

                MessageBox.Show("Конфигурация сохранена", "Сохранение",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void FolderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void HostTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ProtocolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProtocolComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string protocol = selectedItem.Content.ToString();

                // Управляем видимостью кнопки "Папка"
                FolderButton.Visibility = protocol == "Shared Folder" ? Visibility.Visible : Visibility.Collapsed;

                // Устанавливаем путь по умолчанию в зависимости от протокола
                switch (protocol)
                {
                    case "Shared Folder":
                        PathTextBox.Text = ""; // Очищаем или оставляем пустым
                        break;

                    case "Local":
                        PathTextBox.Text = @"C:\Program Files";
                        break;

                    case "SSH":
                        PathTextBox.Text = "/var/log/CK-11";
                        break;
                }
            }
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void PortTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void PasswordBox_PreviewMouseLeftButtonDown(object sender, TextChangedEventArgs e)
        {

        }

        private void PasswordBox_PasswordChanged(object sender, TextChangedEventArgs e)
        {

        }


        // Обработчик нажатия левой кнопки мыши
        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Запоминаем элемент и время клика
            _lastClickedItem = ServersTreeView.SelectedItem;
            _lastTreeViewClickTime = DateTime.Now;
        }

        // Обработчик отпускания кнопки мыши
        private void TreeView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Проверяем двойной клик (интервал < 300ms и тот же элемент)
            if ((DateTime.Now - _lastTreeViewClickTime).TotalMilliseconds < 300 &&
                ServersTreeView.SelectedItem == _lastClickedItem)
            {
                // Ваш код для двойного клика
                var selectedItem = ServersTreeView.SelectedItem;
                //MessageBox.Show($"Двойной клик на элементе: {selectedItem} Добавить в комбобокс");

                // Или можно вызвать метод обработки:
                // ProcessTreeViewDoubleClick(selectedItem);
            }
        }
        // 1. Обработчик PreviewMouseLeftButtonDown
        private void PasswordBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Проверка двойного клика (интервал < 300 мс)
            if ((DateTime.Now - _lastPasswordBoxClickTime).TotalMilliseconds < 300)
            {
                // Действия при двойном клике
                TogglePasswordVisibility();
                e.Handled = true; // Предотвращаем дальнейшую обработку
            }

            _lastPasswordBoxClickTime = DateTime.Now;
        }

        // 2. Обработчик PasswordChanged
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {

            string password = PasswordBox.Password;
            // ... дополнительная обработка пароля
        }

        // Дополнительный метод для переключения видимости пароля
        private void TogglePasswordVisibility()
        {
            // Создаем временное TextBox для показа пароля
            var passwordContainer = PasswordBox.Parent as Panel;
            if (passwordContainer == null) return;

            var tempTextBox = new TextBox
            {
                Text = PasswordBox.Password,
                Margin = PasswordBox.Margin,
                Width = PasswordBox.ActualWidth
            };

            // Заменяем PasswordBox на TextBox
            passwordContainer.Children.Remove(PasswordBox);
            passwordContainer.Children.Add(tempTextBox);

            // Возвращаем PasswordBox через 3 секунды
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            timer.Tick += (s, args) =>
            {
                passwordContainer.Children.Remove(tempTextBox);
                passwordContainer.Children.Add(PasswordBox);
                timer.Stop();
            };

            timer.Start();
        }

        private void DuplicateConnection_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is TreeViewItem selectedItem &&
                selectedItem.Tag is ServerConfig selectedServer)
            {
                // Находим родительскую папку
                var parentFolder = FindParentFolder(selectedServer);

                if (parentFolder != null)
                {
                    // Создаем копию сервера
                    var duplicatedServer = new ServerConfig
                    {
                        Name = $"{selectedServer.Name} (копия)",
                        Host = selectedServer.Host,
                        Protocol = selectedServer.Protocol,
                        Username = selectedServer.Username,
                        Password = selectedServer.Password,
                        Path = selectedServer.Path,
                        Port = selectedServer.Port
                    };

                    // Добавляем копию в ту же папку
                    parentFolder.Servers.Add(duplicatedServer);

                    // Обновляем TreeView
                    LoadServersTree();

                    // Выбираем новый элемент
                    SelectServerInTree(duplicatedServer);
                }
            }
        }

        private void SelectServerInTree(ServerConfig server)
        {
            foreach (var folderItem in ServersTreeView.Items.OfType<TreeViewItem>())
            {
                if (folderItem.Tag is ServerFolder folder && folder.Servers.Contains(server))
                {
                    foreach (var serverItem in folderItem.Items.OfType<TreeViewItem>())
                    {
                        if (serverItem.Tag == server)
                        {
                            serverItem.IsSelected = true;
                            serverItem.BringIntoView();
                            break;
                        }
                    }
                    break;
                }
            }
        }

        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

            if (treeViewItem != null)
            {
                treeViewItem.IsSelected = true;
                e.Handled = true;
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private void OpenSharedFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем хост из текстового поля
                string host = HostTextBox.Text.Trim();

                if (string.IsNullOrEmpty(host))
                {
                    System.Windows.MessageBox.Show("Введите хост или сетевой путь");
                    return;
                }

                // Формируем начальный путь
                string initialPath = $"\\\\{host}\\";

                var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    InitialDirectory = initialPath // Например: "\\192.168.1.100\Shared"
                };

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string fullPath = dialog.FileName;
                    string rootPath = $"\\\\{HostTextBox.Text}\\"; // "\\192.168.1.100\"

                    // Обрезаем начальную часть пути (если начинается с rootPath)
                    if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = fullPath.Substring(rootPath.Length);
                        PathTextBox.Text = relativePath; // "Shared\Folder\Subfolder"
                    }
                    else
                    {
                        PathTextBox.Text = fullPath; // Если не совпадает, оставляем полный путь
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
            }

        }
    }

}
