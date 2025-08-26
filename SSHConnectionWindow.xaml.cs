using Microsoft.WindowsAPICodePack.Net;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GetStatistics
{
    public partial class SSHConnectionWindow : Window
    {
        private readonly ConfigService _configService;
        private List<SshConnectionConfig> _connections = new List<SshConnectionConfig>();
        private MainWindow _mainWindow;
        private bool _isEditing = false;
        private string _originalConnectionName = string.Empty;

        public SSHConnectionWindow()
        {
            InitializeComponent();
            _configService = new ConfigService();
            _mainWindow = Application.Current.MainWindow as MainWindow;
            Loaded += SSHConnectionWindow_Loaded;
        }

        private void SSHConnectionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConnections();
            ResetForm();
        }

        private void LoadConnections()
        {
            try
            {
                var config = _configService.LoadConfig();
                _connections = config.SshConnections ?? new List<SshConnectionConfig>();
                dgConnections.ItemsSource = _connections;
                dgConnections.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки подключений: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetForm()
        {
            _isEditing = false;
            _originalConnectionName = string.Empty;
            txtConnectionName.Text = "";
            txtHost.Text = "";
            txtPort.Text = "22";
            txtUsername.Text = "administrator";
            txtPassword.Password = "";
            txtPath.Text = "/var/log/CK-11";
            dgConnections.SelectedItem = null;
            btnSave.Content = "Сохранить";
            btnDuplicate.IsEnabled = false;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtConnectionName.Text))
            {
                MessageBox.Show("Введите имя подключения", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtHost.Text))
            {
                MessageBox.Show("Введите хост", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Введите имя пользователя", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true; // Убрали проверку уникальности, так как теперь она обрабатывается отдельно
        }

        private void ConnectionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (dgConnections.SelectedItem is SshConnectionConfig selected)
            {
                _isEditing = true;
                _originalConnectionName = selected.Name;

                txtConnectionName.Text = selected.Name;
                txtHost.Text = selected.Host;
                txtPort.Text = selected.Port.ToString();
                txtUsername.Text = selected.Username;
                txtPassword.Password = selected.Password;
                txtPath.Text = selected.Path;

                btnSave.Content = "Обновить";
                btnDuplicate.IsEnabled = true;
            }
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || !ValidateForm()) return;

            try
            {
                _mainWindow.isLocal = false;
                var serverConfig = new ServerConfig
                {
                    Host = txtHost.Text,
                    Name = txtConnectionName.Text,
                    Username = txtUsername.Text,
                    Password = txtPassword.Password,
                    Path = txtPath.Text
                };

                // Проверяем, существует ли уже такое подключение
                var existingConnection = _connections.FirstOrDefault(c =>
                    c.Name.Equals(serverConfig.Name, StringComparison.OrdinalIgnoreCase));

                // Если подключение не существует, предлагаем сохранить
                if (existingConnection == null)
                {
                    var result = MessageBox.Show("Подключение не сохранено. Хотите сохранить его перед подключением?",
                        "Сохранение подключения",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel)
                        return;

                    if (result == MessageBoxResult.Yes)
                    {
                        // Сохраняем подключение
                        var connection = new SshConnectionConfig
                        {
                            Name = txtConnectionName.Text.Trim(),
                            Host = txtHost.Text.Trim(),
                            Port = int.TryParse(txtPort.Text, out var port) ? port : 22,
                            Username = txtUsername.Text.Trim(),
                            Password = txtPassword.Password,
                            Path = txtPath.Text.Trim()
                        };

                        _configService.AddOrUpdateConnection(connection);
                        LoadConnections();

                        MessageBox.Show("Подключение сохранено", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                // Подключаемся
                var logFiles = await _mainWindow.ConnectViaSsh(serverConfig);
                _mainWindow._currentLogFolderPath = "";
                _mainWindow.UpdateLogList(logFiles);
                _mainWindow.AddLogResultInDataGrid($"SSH: {serverConfig.Name} {serverConfig.Host}");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка SSH",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                var connection = new SshConnectionConfig
                {
                    Name = txtConnectionName.Text.Trim(),
                    Host = txtHost.Text.Trim(),
                    Port = int.TryParse(txtPort.Text, out var port) ? port : 22,
                    Username = txtUsername.Text.Trim(),
                    Password = txtPassword.Password,
                    Path = txtPath.Text.Trim()
                };

                _configService.AddOrUpdateConnection(connection);
                LoadConnections();
                ResetForm();

                MessageBox.Show($"Подключение {(_isEditing ? "обновлено" : "сохранено")}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is SshConnectionConfig selected)
            {
                // Создаем копию с суффиксом "_copy"
                var duplicate = new SshConnectionConfig
                {
                    Name = $"{selected.Name}_copy",
                    Host = selected.Host,
                    Port = selected.Port,
                    Username = selected.Username,
                    Password = selected.Password,
                    Path = selected.Path
                };

                // Находим уникальное имя
                int copyNumber = 1;
                while (_connections.Any(c => c.Name.Equals(duplicate.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    duplicate.Name = $"{selected.Name}_copy{copyNumber++}";
                }

                // Заполняем форму данными дубликата
                _isEditing = false;
                _originalConnectionName = string.Empty;

                txtConnectionName.Text = duplicate.Name;
                txtHost.Text = duplicate.Host;
                txtPort.Text = duplicate.Port.ToString();
                txtUsername.Text = duplicate.Username;
                txtPassword.Password = duplicate.Password;
                txtPath.Text = duplicate.Path;

                btnSave.Content = "Сохранить";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Выбор пути...", "Путь", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddNew_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        private void EditConnection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SshConnectionConfig connection)
            {
                dgConnections.SelectedItem = connection;
            }
        }

        private void DeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is SshConnectionConfig selected)
            {
                if (MessageBox.Show($"Удалить подключение '{selected.Name}'?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _configService.DeleteConnection(selected.Name);
                        LoadConnections();
                        ResetForm();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}