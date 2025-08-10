using LogNavigator.Models;
using System.Windows;

namespace GetStatistics
{
    public partial class SharedFolderEditDialog : Window
    {
        // Публичное свойство для доступа к данным
        public SharedFolder Folder { get; private set; }

        public SharedFolderEditDialog(SharedFolder folder = null)
        {
            InitializeComponent();
            Folder = folder ?? new SharedFolder();
            DataContext = this;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                DialogResult = true;
                Close();
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(Folder.ServerName))
            {
                MessageBox.Show("Введите имя сервера", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!Folder.SharePath.StartsWith(@"\\"))
            {
                MessageBox.Show("Сетевой путь должен начинаться с \\\\", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}