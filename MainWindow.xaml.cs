using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Renci.SshNet;
using AxMSTSCLib;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;
using GetStatistics.Models;
using static FilterLogFile;
using System.Windows.Media;
using System.Text;


namespace GetStatistics
{
    public partial class MainWindow : Window
    {
        public bool isLocal = true;
        private readonly PathCombine _pathCombine;
        private Config _config;
        private List<string> _logFiles = new List<string>();
        private bool _filterByToday = false;
        private NetworkConnection _networkConnection;
        private string _searchText = "";
        private string _searchLogText = "";
        private string _searchLogTextRight = "";
        private FilterFiles _filterFiles;
        private LogFileService _logFileService;
        private readonly QuoteManager _quoteManager;
        private ConfigLoader _configLoader;
        private FilterLogFile _filterLogFile;
        public SshClient _sshClient;
        private bool _isReadingLogs = false;
        private string _currentLogFilePath = "";
        private string _currentLogDirectory; // Путь к папке с логами
        private string _currentLogFile;     // Только имя файла
        public string _currentLogFolderPath; // Хранит только путь к папке

        public MainWindow()
        {
            InitializeComponent();
            _configLoader = new ConfigLoader(
                StringComboBox_One_Left,
                StringComboBox_Two_Left,
                StringComboBox_One_Right,
                StringComboBox_Two_Right);
            LoadConfig();
            _networkConnection = new NetworkConnection();
            _filterFiles = new FilterFiles(this);
            _filterLogFile = new FilterLogFile(LogRichTextBox, this);
            _logFileService = new LogFileService(
                LogRichTextBox,
                StatusText,
                () => new FilterParameters
                {
                    Filter_One = StringComboBox_One_Left.SelectedItem?.ToString(),
                    Filter_Two = StringComboBox_Two_Left.SelectedItem?.ToString(),
                    SearchText_One = SearchTextBoxLog_One_Left.Text,
                    SearchText_Two = SearchTextBoxLog_Two_Left.Text
                },
                () => new FilterParameters
                {
                    Filter_One = StringComboBox_One_Right.SelectedItem?.ToString(),
                    Filter_Two = StringComboBox_Two_Right.SelectedItem?.ToString(),
                    SearchText_One = SearchTextBoxLog_One_Right.Text,
                    SearchText_Two = SearchTextBoxLog_Two_Right.Text
                },
                this  // ← теперь правильно
            );

            _pathCombine = new PathCombine();

            _quoteManager = new QuoteManager("quotes.txt");
            Loaded += OnMainWindowLoaded;

            StatusText.Text = $"  Team78 (UAT)";
        }

        public async void LoadConfig()
        {
            await _configLoader.LoadConfigAsync("config.json");
        }

        public ObservableCollection<LogResult> LogResults { get; } = new ObservableCollection<LogResult>();

        private async Task GetLocalFiles(string path)
        {
            try
            {
                var patterns = new[] { "*.log", "*.usrlog", "*.txt" };
                _logFiles = patterns.SelectMany(p => Directory.GetFiles(path, p))
                                    .Distinct()
                                    .ToList();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async Task<List<string>> GetLogFilesFromServerAsync(ServerConfig server)
        {
            var logFiles = new List<string>();

            if (_sshClient != null && _sshClient.IsConnected)
            {
                var command = _sshClient.CreateCommand($"ls {server.Path} | grep -E '\\log$|\\.txt$'");
                var result = await Task.Run(() => command.Execute());

                if (command.ExitStatus == 0)
                {
                    logFiles = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(file => Path.Combine(server.Path, file))
                                    .ToList();
                }
                else
                {
                    MessageBox.Show($"Ошибка получения списка файлов: {command.Error}");
                }
            }

            return logFiles;
        }
        public void UpdateLogList(List<string> filePaths)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Сохраняем полные пути
                    _logFiles = filePaths;

                    // Отображаем только имена файлов
                    LogList.ItemsSource = filePaths.Select(Path.GetFileName).ToList();

                    StatusText.Text = $"Найдено {filePaths.Count} файлов";

                    // Сбрасываем текущий выбранный файл
                    _currentLogFilePath = string.Empty;
                    LogList.UnselectAll();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    StatusText.Text = $"Ошибка обновления списка: {ex.Message}");
            }
        }
        // Фильтр файлов по условию
        public async void ApplyFilters()
        {
            var filteredFiles = _logFiles;

            // Логи за сегодня
            if (_filterByToday)
            {
                filteredFiles = await _filterFiles.FilterByToday(filteredFiles);
            }

            // Фильтр логов по названию
            if (!string.IsNullOrEmpty(_searchText))
            {
                filteredFiles = await _filterFiles.FilterFilesByName(filteredFiles, _searchText);
            }

            var fileNames = filteredFiles.Select(Path.GetFileName).ToList();
            LogList.ItemsSource = fileNames;
            Debug.WriteLine($"Отображаем файлы: {fileNames.Count}");
        }

        private void StopReadingLogs()
        {
            try
            {
                // 1. Остановка фонового чтения логов
                _isReadingLogs = false;

                //// 2. Закрытие SSH-соединения
                //if (_sshClient != null && _sshClient.IsConnected)
                //{
                //    _sshClient.Disconnect();
                //    _sshClient.Dispose();
                //    _sshClient = null;
                //}

                //// 3. Очистка текущих данных
                //_logFiles.Clear();
                //_currentLogFilePath = string.Empty;

                //// 4. Обновление UI
                //Dispatcher.Invoke(() =>
                //{
                //    LogListBox.ItemsSource = null;
                //    LogRichTextBox.Document.Blocks.Clear();
                //});

                //Debug.WriteLine("Чтение логов остановлено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при остановке чтения логов: {ex.Message}");
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Ошибка при остановке: {ex.Message}"));
            }
        }

        private async void LogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(LogList.SelectedItem is string selectedFileName)) return;

            try
            {
                if (_sshClient != null && _sshClient.IsConnected)
                {
                    _logFiles = _logFiles.Select(f => f.Replace('\\', '/')).ToList();
                    // Для SSH используем полный путь из _logFiles
                    _currentLogFilePath = _logFiles.FirstOrDefault(f =>
                        Path.GetFileName(f).Equals(selectedFileName, StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrEmpty(_currentLogFilePath))
                    {
                        MessageBox.Show("Файл не найден на сервере", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await _logFileService.LoadLogFile(
                        _currentLogFilePath,
                        new ServerConfig { Protocol = "SSH" },
                        _sshClient
                    );
                }
                else
                {
                    // Для локальных файлов
                    _currentLogFilePath = Path.Combine(_currentLogFolderPath, selectedFileName);
                    await _logFileService.LoadLogFile(
                        _currentLogFilePath,
                        new ServerConfig { Protocol = "Local" }
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckComboBoxes()
        {
            if (!string.IsNullOrEmpty(StringComboBox_One_Left.Text) &&
                    string.IsNullOrEmpty(StringComboBox_Two_Left.Text) &&
                    string.IsNullOrEmpty(SearchTextBoxLog_One_Left.Text) &&
                    string.IsNullOrEmpty(SearchTextBoxLog_Two_Left.Text) &&
                    string.IsNullOrEmpty(StringComboBox_One_Right.Text) &&
                    string.IsNullOrEmpty(StringComboBox_Two_Right.Text) &&
                    string.IsNullOrEmpty(SearchTextBoxLog_One_Right.Text) &&
                    string.IsNullOrEmpty(SearchTextBoxLog_Two_Right.Text))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        private async Task LoadLocalFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                MessageBox.Show("Путь к файлу не указан.");
                return;
            }

            if (!File.Exists(filePath))
            {
                MessageBox.Show($"Файл не существует: {filePath}");
                return;
            }

            try
            {
                // Пытаемся открыть файл с задержкой и повторными попытками
                int maxRetries = 3;
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        using (var fileStream = new FileStream(
                            filePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite)) // Разрешаем чтение, даже если файл заблокирован
                        using (var streamReader = new StreamReader(fileStream))
                        {
                            string content = await streamReader.ReadToEndAsync();
                            LogRichTextBox.Document.Blocks.Clear();
                            _logFileService.ApplyLogFilters(content, LogRichTextBox, true);
                            return; // Успешно
                        }
                    }
                    catch (IOException ex) when (i < maxRetries - 1)
                    {
                        await Task.Delay(100); // Ждём 100 мс перед повторной попыткой
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Нет доступа к файлу: {ex.Message}\nПопробуйте запустить программу от имени администратора.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения файла: {ex.Message}");
            }
        }


        private void TodayCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _filterByToday = true;
            ApplyFilters();
        }

        private void TodayCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _filterByToday = false;
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchTextBox.Text;
            ApplyFilters();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Вызываем тот же метод, что и при изменении текста
                //_searchText = SearchTextBox.Text;
                ApplyFilters();
            }
        }
        private async void SearchTextBoxLog_One_Left_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                //if (!string.IsNullOrEmpty(_currentLogFilePath) && ServerComboBox.SelectedItem is ServerConfig server)
                //{
                //    await _logFileService.LoadLogFile(_currentLogFilePath, server);
                //    StatusText.Text = "Сортировка по левому фильтру...";
                //}
                //else
                //{
                await LoadLocalFile(_currentLogFilePath);
                StatusText.Text = "Сортировка по левому фильтру...";
                StartSearchInFilesButton_Left_Click(sender, e);
                //}
            }
        }

        private async void SearchTextBoxLogRight_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                //if (!string.IsNullOrEmpty(_currentLogFilePath) && ServerComboBox.SelectedItem is ServerConfig server)
                //{
                //    await _logFileService.LoadLogFile(_currentLogFilePath, server);
                //    StatusText.Text = "Сортировка по правому фильтру...";

                //}
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _sshClient?.Disconnect();
            _sshClient?.Dispose();
            base.OnClosed(e);
        }


        private void Time1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Time2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void RadioButtonSSH_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void RadioButtonRDP_Checked(object sender, RoutedEventArgs e)
        {

        }

        private async void StringTwoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void StringOneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void StringOneComboBox_Right_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void StringTwoComboBox_Right_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SearchInFile_LeftButton_Click(object sender, RoutedEventArgs e)
        {
            // Метод с левыми фильтрами
        }


        private void ClearButton_Right_Click(object sender, RoutedEventArgs e)
        {
            StringComboBox_One_Right.Text = "";
            StringComboBox_Two_Right.Text = "";
            SearchTextBoxLog_One_Right.Text = "";
            SearchTextBoxLog_Two_Right.Text = "";
            StatusText.Text = "Фильтр справа очищен";
        }
        private void LogRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void LogRichTextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private string ExtractTimestamp(string input)
        {
            // Регулярное выражение для поиска timestamp с миллисекундами
            // Пример формата: 2023-04-15 14:30:45.123
            Regex regex = new Regex(@"\d{2}:\d{2}:\d{2}\.\d{3}");

            Match match = regex.Match(input);

            return match.Success ? match.Value : null;
        }
        bool boolTimeStamp = false;
        private ObservableCollection<LogResult> _logResults = new ObservableCollection<LogResult>();
        string lineText1 = "";
        string lineText2 = "";

        private string GetSelectedLineText()
        {
            var textPointer = LogRichTextBox.GetPositionFromPoint(
                Mouse.GetPosition(LogRichTextBox), true);
            if (textPointer == null) return null;

            var lineStart = GetLineStart(textPointer);
            var lineEnd = GetLineEnd(textPointer);

            return lineStart != null && lineEnd != null
                ? new TextRange(lineStart, lineEnd).Text.Trim()
                : null;
        }



        // Получает начало строки для данного TextPointer
        private TextPointer GetLineStart(TextPointer pointer)
        {
            var lineStart = pointer;
            while (lineStart != null && lineStart.GetPointerContext(LogicalDirection.Backward) != TextPointerContext.ElementStart)
            {
                lineStart = lineStart.GetNextContextPosition(LogicalDirection.Backward);
            }
            return lineStart;
        }

        // Получает конец строки для данного TextPointer
        private TextPointer GetLineEnd(TextPointer pointer)
        {
            var lineEnd = pointer;
            while (lineEnd != null && lineEnd.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.ElementEnd)
            {
                lineEnd = lineEnd.GetNextContextPosition(LogicalDirection.Forward);
            }
            return lineEnd;
        }


        private void LogRichTextBox_MouseMove(object sender, MouseEventArgs e)
        {

            //var clickPosition = e.GetPosition(LogRichTextBox);
            //var textPointer = LogRichTextBox.GetPositionFromPoint(clickPosition, true);

            //if (textPointer == null)
            //    return;

            //// Получаем начало строки
            //var lineStart = GetLineStart(textPointer);
            //var lineEnd = GetLineEnd(textPointer);

            //if (lineStart == null || lineEnd == null)
            //    return;
            //LogRichTextBox.Selection.Select(lineStart, lineEnd);

        }

        private void CopyAllTableButton_Click(object sender, RoutedEventArgs e)
        {
            _filterLogFile.CopyResultsToClipboard();

            //try
            //{
            //    if (_logResults == null || _logResults.Count == 0)
            //    {
            //        MessageBox.Show("Нет данных для копирования");
            //        return;
            //    }

            //    // Создаем строку с заголовками колонок
            //    var headers = string.Join("\t", ResultsDataGrid.Columns
            //        .Where(c => c.Header != null && c.Header.ToString() != "⎘")
            //        .Select(c => c.Header.ToString()));

            //    // Собираем все данные
            //    var dataLines = _logResults.Select(row =>
            //        $"{row.LineText1}\n{row.LineText2}\n{row.Result}\n");

            //    // Объединяем в один текст
            //    var allText = string.Join(Environment.NewLine, dataLines);

            //    // Копируем в буфер обмена
            //    Clipboard.SetText(allText);

            //    // Показываем уведомление
            //    ShowCopyNotification("Все данные скопированы!");
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Ошибка при копировании: {ex.Message}");
            //}
        }

        // Копирование по кнопке
        private void CopyRowButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultsDataGrid.SelectedItem as FilterResultItem;
            if (selectedItem != null)
            {
                _filterLogFile.CopySingleRowToClipboard(selectedItem);
                ShowCopyNotification();
            }
        }

        // Копирование по двойному клику на строку
        private void ResultsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is LogResult row)
            {
                Clipboard.SetText(row.GetCopyText());
                ShowCopyNotification();
            }
        }

        // Всплывающее уведомление
        private void ShowCopyNotification()
        {
            var notification = new ToolTip
            {
                Content = "Данные скопированы!",
                StaysOpen = false,
                IsOpen = true,
                Placement = PlacementMode.Mouse
            };

            // Автоматическое закрытие через 1 секунду
            Task.Delay(1000).ContinueWith(_ =>
                Dispatcher.Invoke(() => notification.IsOpen = false));
        }

        public class LogResult
        {
            public string Filters { get; set; }  // Лог строка 1
            public string Counter { get; set; } // Лог строка 2
            public string FullCounter { get; set; } // Числовой результат
            public string GetCopyText() => $"{Filters}\n{Counter}\n{FullCounter}";
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void SearchTextBoxLog_Two_Left_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private string OpenFolderDialog()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку с логами",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.SelectedPath;
            }

            return null; // или string.Empty, если выбор отменен
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            _currentLogFolderPath = null;
            _currentLogFilePath = null;
            if (_sshClient != null && _sshClient.IsConnected)
            {
                try
                {
                    _sshClient.Disconnect();
                    _sshClient.Dispose();
                    _sshClient = null;
                    Console.WriteLine("SSH-соединение было закрыто.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при отключении SSH: {ex.Message}");
                }
            }
            string selectedFolder;

            // Определяем, какая кнопка вызвала событие
            if (sender == OpenFolderCK11)
            {
                // Фиксированный путь для кнопки CK-11
                selectedFolder = @"C:\Program Files\Monitel\CK-11\Client\Log";

                // Проверяем существование папки
                if (!Directory.Exists(selectedFolder))
                {
                    MessageBox.Show($"Папка не найдена: {selectedFolder}");
                    return;
                }
            }
            else
            {
                // Для обычной кнопки открываем диалог выбора папки
                selectedFolder = OpenFolderDialog();

                if (string.IsNullOrEmpty(selectedFolder))
                {
                    MessageBox.Show("Выбор папки отменён.");
                    return;
                }
            }

            _currentLogFolderPath = selectedFolder;
            Console.WriteLine(_currentLogFolderPath);
            await GetLocalFiles(_currentLogFolderPath);
            ApplyFilters();
        }

        public void UpdateStatusText(string text)
        {
            Dispatcher.Invoke(() => StatusText.Text = text);
        }

        private void SearchTextBoxLog_One_Left_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        public async Task<List<string>> ConnectViaSsh(ServerConfig server)
        {
            try
            {
                // Закрываем предыдущее подключение
                if (_sshClient != null)
                {
                    _sshClient.Disconnect();
                    _sshClient.Dispose();
                }

                // Создаем новое подключение
                _sshClient = new SshClient(server.Host, server.Username, server.Password);
                await Task.Run(() => _sshClient.Connect());

                if (_sshClient.IsConnected)
                {
                    var command = _sshClient.CreateCommand($"ls {server.Path} | grep -E '\\.log$|\\.txt$'");
                    var result = await Task.Run(() => command.Execute());

                    if (command.ExitStatus == 0)
                    {
                        return result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(file => Path.Combine(server.Path, file))
                                   .ToList();
                    }
                    throw new Exception($"Ошибка выполнения команды: {command.Error}");
                }
                throw new Exception("Не удалось подключиться к SSH");
            }
            catch (Exception ex)
            {
                _sshClient?.Dispose();
                _sshClient = null;
                StatusText.Text = $"Ошибка SSH: {ex.Message}";
                throw; // Перебрасываем исключение для обработки в вызывающем коде
            }
        }

        public void UpdateFileList(List<string> files)
        {
            Dispatcher.Invoke(() =>
            {
                _logFiles = files;
                LogList.ItemsSource = files.Select(Path.GetFileName).ToList();
                StatusText.Text = $"Найдено {files.Count} файлов";
            });
        }


        private void SearchTextBoxLog_Two_Left_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                StartSearchInFilesButton_Left_Click(sender, e);
            }
        }

        private async void StartSearchInFilesButton_Left_Click(object sender, RoutedEventArgs e)
        {
            StringCounter_Left.Content = "";
            StringCounter_Main.Content = "";
            if (string.IsNullOrEmpty(_currentLogFilePath))
                return;

            _isReadingLogs = false;

            try
            {
                string content;

                if (_sshClient != null && _sshClient.IsConnected)
                {
                    // Чтение файла через SSH (для Linux-сервера)
                    content = await ReadFileViaSsh(_currentLogFilePath);
                }
                else
                {
                    // Чтение локального файла (старый код)
                    Console.WriteLine(_currentLogFilePath);
                    content = await ReadLocalFile(_currentLogFilePath);
                }

                _logFileService.ApplyLogFilters(content, LogRichTextBox, isLeftFilter: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось прочитать файл логов: {ex.Message}",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        // Метод для чтения файла через SSH
        private async Task<string> ReadFileViaSsh(string filePath)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("SSH-соединение не установлено.");

            // Убедимся, что путь использует `/` для Linux
            filePath = filePath.Replace('\\', '/');

            // Выполняем команду `cat` для чтения файла
            var command = _sshClient.CreateCommand($"cat '{filePath}'");
            var result = await Task.Factory.FromAsync(command.BeginExecute(), command.EndExecute);

            if (command.ExitStatus != 0)
            {
                throw new IOException($"Ошибка чтения файла: {result}");
            }

            return result;
        }

        // Метод для чтения локального файла (старая логика)
        private async Task<string> ReadLocalFile(string filePath)
        {
            return await Task.Run(() =>
            {
                using (var fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(fileStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            });
        }

        private async void StartSearchInFilesButton_Right_Click(object sender, RoutedEventArgs e)
        {
            StringCounter_Left.Content = "";
            StringCounter_Main.Content = "";
            if (string.IsNullOrEmpty(_currentLogFilePath))
                return;

            _isReadingLogs = false;

            try
            {
                string content;

                if (_sshClient != null && _sshClient.IsConnected)
                {
                    // Чтение файла через SSH (для Linux-сервера)
                    content = await ReadFileViaSsh(_currentLogFilePath);
                }
                else
                {
                    // Чтение локального файла (старый код)
                    content = await ReadLocalFile(_currentLogFilePath);
                }

                _logFileService.ApplyLogFilters(content, LogRichTextBox, isLeftFilter: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось прочитать файл логов: {ex.Message}",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        // Очистить левые фильтры
        private void ClearButton_Left_Click(object sender, RoutedEventArgs e)
        {
            StringComboBox_One_Left.Text = "";
            StringComboBox_Two_Left.Text = "";
            SearchTextBoxLog_One_Left.Text = "";
            SearchTextBoxLog_Two_Left.Text = "";
            StatusText.Text = "Фильтр слева очищен";
        }

        // Первая строка поиска справа
        private void SearchTextBoxLog_One_Right_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Реализация фильтрации
        }

        // Вторая строка поиска справа
        private void SearchTextBoxLog_Two_Right_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Реализация фильтрации
        }

        private void SearchTextBoxLog_One_Right_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) StartSearchInFilesButton_Right_Click(sender, e);
        }

        private void SearchTextBoxLog_Two_Right_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) StartSearchInFilesButton_Right_Click(sender, e);
        }



        private void DisplayQuoteInRichTextBox(RichTextBox richTextBox, string quote, string author)
        {
            // Очищаем содержимое
            richTextBox.Document.Blocks.Clear();

            // Создаем параграф для цитаты
            var quoteParagraph = new Paragraph
            {
                Margin = new Thickness(0, 10, 0, 20),
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                TextAlignment = TextAlignment.Justify
            };
            quoteParagraph.Inlines.Add(new Run($"\"{quote}\""));

            // Создаем параграф для автора
            var authorParagraph = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 10),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                TextAlignment = TextAlignment.Right
            };
            authorParagraph.Inlines.Add(new Run($"— {author}"));

            // Добавляем параграфы в RichTextBox
            richTextBox.Document.Blocks.Add(quoteParagraph);
            richTextBox.Document.Blocks.Add(authorParagraph);
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            var (quote, author) = _quoteManager.GetRandomQuote();
            DisplayQuoteInRichTextBox(LogRichTextBox, quote, author);
        }

        private void ShowHourlyStatistics_Click(object sender, RoutedEventArgs e)
        {
            // Получаем текст из RichTextBox
            string logText = new TextRange(LogRichTextBox.Document.ContentStart,
                                         LogRichTextBox.Document.ContentEnd).Text;

            // Словарь для хранения количества строк по часам
            Dictionary<int, int> hourlyStats = new Dictionary<int, int>();

            // Инициализируем словарь для всех 24 часов
            for (int hour = 0; hour < 24; hour++)
            {
                hourlyStats[hour] = 0;
            }

            // Разбиваем текст на строки
            string[] lines = logText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Регулярное выражение для поиска временных меток
            Regex timeRegex = new Regex(@"\b(\d{2}):\d{2}:\d{2}(?:\.\d{3})?\b");

            // Анализируем каждую строку
            foreach (string line in lines)
            {
                Match match = timeRegex.Match(line);
                if (match.Success)
                {
                    string timePart = match.Groups[1].Value;
                    if (int.TryParse(timePart, out int hour))
                    {
                        if (hour >= 0 && hour < 24)
                        {
                            hourlyStats[hour]++;
                        }
                    }
                }
            }

            // Находим максимальное количество строк
            int maxCount = hourlyStats.Values.Max();
            if (maxCount == 0) maxCount = 1;

            // Определяем максимальную ширину чисел
            int maxNumberWidth = maxCount.ToString().Length;

            // Создаем FlowDocument
            FlowDocument flowDoc = new FlowDocument
            {
                PagePadding = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                Background = Brushes.White
            };

            // Заголовок
            Paragraph header = new Paragraph(new Run("Статистика по часам:"))
            {
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            flowDoc.Blocks.Add(header);

            // Добавляем статистику с выравниванием
            for (int hour = 0; hour < 24; hour++)
            {
                int count = hourlyStats[hour];
                double percentage = (double)count / maxCount;
                int barLength = (int)(percentage * 20);

                string hourStr = hour.ToString("00");
                string bar = new string('█', barLength);
                string countStr = count.ToString().PadLeft(maxNumberWidth);

                // Формируем строку с фиксированными отступами
                string line = $"{hourStr}:00 |{bar,-20} {countStr}";

                Paragraph para = new Paragraph(new Run(line))
                {
                    Margin = new Thickness(0, 3, 0, 3) // Отступы между строками
                };
                flowDoc.Blocks.Add(para);
            }

            // Создаем FlowDocumentScrollViewer
            FlowDocumentScrollViewer documentViewer = new FlowDocumentScrollViewer
            {
                Document = flowDoc,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Style = (Style)FindResource("ModernFlowDocumentViewer")
            };

            // Создаем кнопку "Копировать"
            Button copyButton = new Button
            {
                Content = "Копировать статистику",
                Margin = new Thickness(5),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)FindResource("ModernButton")
            };

            // Обработчик нажатия на кнопку
            copyButton.Click += (s, args) =>
            {
                // Получаем весь текст из FlowDocument
                string fullText = new TextRange(
                    flowDoc.ContentStart,
                    flowDoc.ContentEnd
                ).Text;

                // Копируем в буфер обмена
                try
                {
                    Clipboard.SetText(fullText);
                    MessageBox.Show("Статистика скопирована в буфер обмена!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка копирования: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Создаем контейнер с кнопкой и содержимым
            DockPanel container = new DockPanel
            {
                LastChildFill = true
            };

            // Размещаем кнопку внизу окна
            DockPanel.SetDock(copyButton, Dock.Bottom);
            container.Children.Add(copyButton);
            container.Children.Add(documentViewer);

            // Создаем окно статистики
            Window statsWindow = new Window
            {
                Title = "Статистика по часам",
                Width = 450,
                Height = 660,
                MinWidth = 400,
                MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)FindResource("LightBackground"),
                Content = container  // Используем контейнер с кнопкой и содержимым
            };

            statsWindow.Show();
        }

        private void OpenSSHConnectionWindow_Click(object sender, RoutedEventArgs e)
        {
            var sshWindow = new SSHConnectionWindow
            {
                Owner = this // Важно установить владельца!
            };
            sshWindow.ShowDialog();
        }

        private async void SearchInAllFilesButton_Click(object sender, RoutedEventArgs e)
        {

            if (_logFiles == null || _logFiles.Count == 0)
            {
                MessageBox.Show("Нет файлов для поиска");
                return;
            }

            try
            {
                StatusProgressBar.Visibility = Visibility.Visible;
                StatusProgressBar.IsIndeterminate = true; // Бесконечная анимация
                StatusText.Text = "Поиск во всех файлах...";
                var results = new StringBuilder();
                int totalFilesWithMatches = 0;
                int totalMatches = 0;

                // Получаем параметры левого фильтра
                var leftFilterParams = new FilterParameters
                {
                    Filter_One = StringComboBox_One_Left.SelectedItem?.ToString(),
                    Filter_Two = StringComboBox_Two_Left.SelectedItem?.ToString(),
                    SearchText_One = SearchTextBoxLog_One_Left.Text,
                    SearchText_Two = SearchTextBoxLog_Two_Left.Text
                };

                // Очищаем RichTextBox перед выводом результатов
                Dispatcher.Invoke(() => LogRichTextBox.Document.Blocks.Clear());

                // Создаем FlowDocument для форматированного вывода
                FlowDocument flowDoc = new FlowDocument();
                Paragraph headerParagraph = new Paragraph(new Run("Результаты поиска во всех файлах:"))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = Brushes.DarkBlue
                };
                flowDoc.Blocks.Add(headerParagraph);

                // Проходим по всем файлам
                foreach (var filePath in _logFiles)
                {
                    try
                    {
                        string content;

                        // Читаем файл в зависимости от типа подключения
                        if (_sshClient != null && _sshClient.IsConnected)
                        {
                            content = await ReadFileViaSsh(filePath);
                        }
                        else
                        {
                            content = await ReadLocalFile(filePath);
                        }

                        // Разбиваем содержимое на строки
                        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        int fileMatchCount = 0;

                        // Проверяем каждую строку на соответствие левому фильтру
                        foreach (var line in lines)
                        {
                            if (_logFileService.MatchesFilter(line, leftFilterParams))
                            {
                                fileMatchCount++;
                            }
                        }

                        if (fileMatchCount > 0)
                        {
                            string fileName = Path.GetFileName(filePath);
                            Paragraph resultParagraph = new Paragraph();
                            resultParagraph.Inlines.Add(new Run($"{fileName}: ")
                            {
                                FontWeight = FontWeights.Bold
                            });
                            resultParagraph.Inlines.Add(new Run($"{fileMatchCount} совпадений"));

                            flowDoc.Blocks.Add(resultParagraph);
                            totalFilesWithMatches++;
                            totalMatches += fileMatchCount;
                        }
                    }
                    catch (Exception ex)
                    {
                        Paragraph errorParagraph = new Paragraph(new Run($"Ошибка обработки файла {Path.GetFileName(filePath)}: {ex.Message}"))
                        {
                            Foreground = Brushes.Red
                        };
                        flowDoc.Blocks.Add(errorParagraph);
                    }
                    
                }

                // Добавляем итоговую статистику
                Paragraph summaryParagraph = new Paragraph();
                summaryParagraph.Inlines.Add(new Run("\nИтоговая статистика:\n")
                {
                    FontWeight = FontWeights.Bold
                });
                summaryParagraph.Inlines.Add(new Run($"Файлов с совпадениями: {totalFilesWithMatches}\n"));
                summaryParagraph.Inlines.Add(new Run($"Всего совпадений: {totalMatches}")
                {
                    FontWeight = FontWeights.Bold
                });
                flowDoc.Blocks.Add(summaryParagraph);

                // Выводим результаты в RichTextBox
                Dispatcher.Invoke(() =>
                {
                    LogRichTextBox.Document = flowDoc;
                    StatusText.Text = $"Поиск завершен. Найдено {totalMatches} совпадений в {totalFilesWithMatches} файлах";
                    StringCounter_Left.Content = $"Найдено: {totalMatches} ";
                    StringCounter_Main.Content = $"Файлов: {totalFilesWithMatches}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    StatusText.Text = $"Ошибка при поиске во всех файлах: {ex.Message}");
            }
            finally
            {
                // Скрываем индикатор после завершения (успешного или с ошибкой)
                StatusProgressBar.Visibility = Visibility.Collapsed;
                StatusProgressBar.IsIndeterminate = false;
            }
        }

        private void Unical_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
                SearchTextBoxLog_One_Left.Background = Brushes.LemonChiffon;
                SearchTextBoxLog_One_Left.BorderThickness = new Thickness(2);
                SearchTextBoxLog_Two_Left.Background = Brushes.LemonChiffon;
                SearchTextBoxLog_Two_Left.BorderThickness = new Thickness(2);
                SearchTextBoxLog_One_Right.Background = Brushes.LightBlue;
                SearchTextBoxLog_One_Right.BorderThickness = new Thickness(2);
                SearchTextBoxLog_Two_Right.Background = Brushes.LightBlue;
                SearchTextBoxLog_Two_Right.BorderThickness = new Thickness(2);
        }
        private void Unical_CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
                SearchTextBoxLog_One_Left.Background = Brushes.White;
                SearchTextBoxLog_One_Left.BorderThickness = new Thickness(1);
                SearchTextBoxLog_Two_Left.Background = Brushes.White;
                SearchTextBoxLog_Two_Left.BorderThickness = new Thickness(1);
                SearchTextBoxLog_One_Right.Background = Brushes.White;
                SearchTextBoxLog_One_Right.BorderThickness = new Thickness(1);
                SearchTextBoxLog_Two_Right.Background = Brushes.White;
                SearchTextBoxLog_Two_Right.BorderThickness = new Thickness(1);
        }

        private void CalculatorMode_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            LeftBorder.Background = Brushes.LemonChiffon;
            LeftBorder.BorderThickness = new Thickness(2);
            RightBorder.Background = Brushes.LightBlue;
            RightBorder.BorderThickness = new Thickness(2);
            CalculatorTab.IsSelected = true;
        }

        private void CalculatorMode_CheckBox_Unhecked(object sender, RoutedEventArgs e)
        {
            LeftBorder.Background = Brushes.White;
            LeftBorder.BorderThickness = new Thickness(1);
            RightBorder.Background = Brushes.White;
            RightBorder.BorderThickness = new Thickness(1);
            ResultsTab.IsSelected = true;
        }
    }
}