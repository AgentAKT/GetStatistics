using Microsoft.WindowsAPICodePack.Net;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace GetStatistics
{
    public partial class SSHConnectionWindow : Window
    {
        public class SshConnection
        {
            public string Name { get; set; }
            public string Host { get; set; }
            public int Port { get; set; } = 22;
            public string Username { get; set; }
            public string Password { get; set; }
            public string Path { get; set; }
        }

        private List<SshConnection> _connections = new List<SshConnection>();
        private MainWindow _mainWindow;
        public SSHConnectionWindow()
        {
            InitializeComponent();
            LoadConnections();
            _mainWindow = new MainWindow();
        }

        private void LoadConnections()
        {
            // Здесь должна быть логика загрузки сохраненных подключений
            // Например, из файла конфигурации или базы данных
            _connections = new List<SshConnection>
            {
                new SshConnection { Name = "UAT.ZES.SCADA1", Host = "10.81.169.53", Username = "administrator", Path = "/var/log/CK-11", Password = "P@ssw0rd" },
                new SshConnection { Name = "UAT.ZES.SCADA2", Host = "10.81.169.54", Username = "administrator", Path = "/var/log/CK-11", Password = "P@ssw0rd" }
            };

            dgConnections.ItemsSource = _connections;
        }

        private void ConnectionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (dgConnections.SelectedItem is SshConnection selected)
            {
                txtConnectionName.Text = selected.Name;
                txtHost.Text = selected.Host;
                txtPort.Text = selected.Port.ToString();
                txtUsername.Text = selected.Username;
                txtPassword.Password = selected.Password;
                txtPath.Text = selected.Path;
            }
        }

        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            // Логика тестирования подключения
            MessageBox.Show("Проверка подключения...", "Тест подключения", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.isLocal = false;
            if (Owner is MainWindow mainWindow)
            {
                try
                {
                    mainWindow.isLocal = false;
                    var serverConfig = new ServerConfig
                    {
                        Host = txtHost.Text,
                        Username = txtUsername.Text,
                        Password = txtPassword.Password,
                        Path = txtPath.Text
                    };

                    var logFiles = await mainWindow.ConnectViaSsh(serverConfig);
                    mainWindow._currentLogFolderPath = ""; // Сбрасываем локальный путь
                    mainWindow.UpdateLogList(logFiles);

                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка SSH", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Логика сохранения подключения
            var connection = new SshConnection
            {
                Name = txtConnectionName.Text,
                Host = txtHost.Text,
                Port = int.TryParse(txtPort.Text, out var port) ? port : 22,
                Username = txtUsername.Text,
                Password = txtPassword.Password,
                Path = txtPath.Text
            };

            // Добавление или обновление в списке
            // Сохранение в файл/БД
            MessageBox.Show("Настройки сохранены", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            // Логика выбора пути
            MessageBox.Show("Выбор пути...", "Путь", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddNew_Click(object sender, RoutedEventArgs e)
        {
            txtConnectionName.Text = "";
            txtHost.Text = "";
            txtPort.Text = "22";
            txtUsername.Text = "";
            txtPassword.Password = "";
            txtPath.Text = "";
            dgConnections.SelectedItem = null;
        }

        private void EditConnection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SshConnection connection)
            {
                dgConnections.SelectedItem = connection;
            }
        }

        private void DeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if (dgConnections.SelectedItem is SshConnection selected)
            {
                if (MessageBox.Show($"Удалить подключение '{selected.Name}'?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _connections.Remove(selected);
                    dgConnections.Items.Refresh();
                }
            }
        }
    }
}