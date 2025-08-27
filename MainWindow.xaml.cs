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
using LogNavigator;
using System.IO.Compression;
using MS.WindowsAPICodePack.Internal;
using System.Net;

//using SharpCompress.Archives;
//using SharpCompress.Common;


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
        private WorkWithCounters _counterHelper;
        private GetLogFiles _getLogFiles;
        public ObservableCollection<LogResult> LogResults { get; } = new ObservableCollection<LogResult>();
        public ObservableCollection<CalculationResultItem> CalcResults { get; } = new ObservableCollection<CalculationResultItem>();
        private List<string> _foundArchives = new List<string>();
        public string ArchivesCountText { get; set; }
        public Visibility ArchivesPanelVisibility { get; set; } = Visibility.Collapsed;
        private string _lastOpenedLocalFolder;
        private readonly ConfigService _configService;
        public bool IsSSHConnected => _sshClient?.IsConnected == true;
        public bool IsLocal => _sshClient == null || !_sshClient.IsConnected;
        private ConfigSearchViewModel _configSearchVM;

        public ConfigSearchViewModel ConfigSearch => _configSearchVM;
        public ICommand AddString1LeftCommand => new RelayCommand(AddString1Left);
        public ICommand AddString2LeftCommand => new RelayCommand(AddString2Left);
        public ICommand AddString1RightCommand => new RelayCommand(AddString1Right);
        public ICommand AddString2RightCommand => new RelayCommand(AddString2Right);

        private void AddString1Left(object parameter)
        {
            if (parameter is string newItem)
            {
                _configSearchVM.AddString1Item(newItem, isLeftSide: true);
            }
        }

        private void AddString2Left(object parameter)
        {
            if (parameter is string newItem)
            {
                _configSearchVM.AddString2Item(newItem, isLeftSide: true);
            }
        }

        private void AddString1Right(object parameter)
        {
            if (parameter is string newItem)
            {
                _configSearchVM.AddString1Item(newItem, isLeftSide: false);
            }
        }

        private void AddString2Right(object parameter)
        {
            if (parameter is string newItem)
            {
                _configSearchVM.AddString2Item(newItem, isLeftSide: false);
            }
        }


        public MainWindow()
        {
            InitializeComponent();
            _configSearchVM = new ConfigSearchViewModel();
            this.DataContext = this;
            _configService = new ConfigService();
            this.DataContext = this;
            _configLoader = new ConfigLoader(
                StringComboBox_One_Left,
                StringComboBox_Two_Left,
                StringComboBox_One_Right,
                StringComboBox_Two_Right);
            LoadConfig();
            ResultsDataGrid.ItemsSource = LogResults;
            ResultsDataGridCalc.ItemsSource = _logCalcResults;
            _networkConnection = new NetworkConnection();
            _filterFiles = new FilterFiles(this);
            _filterLogFile = new FilterLogFile(LogRichTextBox, this);
            _logFileService = new LogFileService(
                LogRichTextBox,
                StatusText,
                () => new FilterParameters
                {
                    Filter_One = StringComboBox_One_Left.Text?.ToString(),
                    Filter_Two = StringComboBox_Two_Left.Text?.ToString(),
                    SearchText_One = SearchTextBoxLog_One_Left.Text,
                    SearchText_Two = SearchTextBoxLog_Two_Left.Text
                },
                () => new FilterParameters
                {
                    Filter_One = StringComboBox_One_Right.Text?.ToString(),
                    Filter_Two = StringComboBox_Two_Right.Text?.ToString(),
                    SearchText_One = SearchTextBoxLog_One_Right.Text,
                    SearchText_Two = SearchTextBoxLog_Two_Right.Text
                },
                this  // ← теперь правильно
            );

            _pathCombine = new PathCombine();
            _quoteManager = new QuoteManager("quotes.txt");
            _counterHelper = new WorkWithCounters(this);
            _getLogFiles = new GetLogFiles();
            Loaded += OnMainWindowLoaded;
            

            StatusText.Text = $"  Team78 (UAT)";
        }

        public async void LoadConfig()
        {
            await _configLoader.LoadConfigAsync("config.json");
        }

        

        public class CalculationResultItem
        {
            public string LineText1 { get; set; }
            public string LineText2 { get; set; }
            public string Result { get; set; }
            public TimeSpan TimeDifference { get; set; }
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
            try
            {
                var filteredFiles = _logFiles;
                string protocol = _sshClient?.IsConnected == true ? "SSH" : "Local";

                // Логи за сегодня
                if (_filterByToday)
                {
                    filteredFiles = await _filterFiles.FilterByToday(filteredFiles, protocol);
                }

                // Фильтр логов по названию
                if (!string.IsNullOrEmpty(_searchText))
                {
                    filteredFiles = await _filterFiles.FilterFilesByName(filteredFiles, _searchText);
                }

                // Обновление UI
                var fileNames = protocol == "SSH"
                    ? filteredFiles.Select(f => Path.GetFileName(f.Replace('\\', '/'))).ToList()
                    : filteredFiles.Select(Path.GetFileName).ToList();

                LogList.Dispatcher.Invoke(() =>
                {
                    LogList.ItemsSource = fileNames;
                    StatusText.Text = $"Найдено файлов: {fileNames.Count}";
                });

                Debug.WriteLine($"Отображаем файлов: {fileNames.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка применения фильтров: {ex.Message}");
                StatusText.Text = "Ошибка фильтрации";
            }
        }

        private async void LogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(LogList.SelectedItem is string selectedFileName)) return;
            AddLogResultInDataGrid($"Файл: {selectedFileName}");
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
                StatusText.Text = _currentLogFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddLogResultInDataGrid(string message)
        {
            if (ResultsTab.IsSelected)
            {
                // Проверяем, что ItemsSource установлен правильно
                if (ResultsDataGrid.ItemsSource == null)
                {
                    ResultsDataGrid.ItemsSource = LogResults;
                }

                LogResults.Add(new LogResult
                {
                    Filters = message,
                    Counter = "/",
                    FullCounter = "/"
                });

                ResultsDataGrid.Items.Refresh();
                ResultsDataGrid.ScrollIntoView(LogResults.Last());
            }
            else if (CalculatorTab.IsSelected)
            {
                // Проверяем, что ItemsSource установлен правильно
                if (ResultsDataGridCalc.ItemsSource == null)
                {
                    ResultsDataGridCalc.ItemsSource = _logCalcResults;
                }

                _logCalcResults.Add(new LogCalcResult
                {
                    LineText1 = message,
                    LineText2 = "/",
                    Result = "/"
                });

                ResultsDataGridCalc.Items.Refresh();
                ResultsDataGridCalc.ScrollIntoView(_logCalcResults.Last());
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
                            _logFileService.ApplyLogFilters(content, LogRichTextBox, true, IsCalculatorMode());
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
                await LoadLocalFile(_currentLogFilePath);
                StatusText.Text = "Сортировка по левому фильтру...";
                StartSearchInFilesButton_Left_Click(sender, e);
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

        private ObservableCollection<LogCalcResult> _logCalcResults = new ObservableCollection<LogCalcResult>();
        string lineCalcText1 = "";
        string lineCalcText2 = "";
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
            if (CalculatorMode_CheckBox.IsChecked == true) 
            { 
            var clickPosition = e.GetPosition(LogRichTextBox);
            var textPointer = LogRichTextBox.GetPositionFromPoint(clickPosition, true);

            if (textPointer == null)
                return;

            // Получаем начало строки
            var lineStart = GetLineStart(textPointer);
            var lineEnd = GetLineEnd(textPointer);

            if (lineStart == null || lineEnd == null)
                return;
            LogRichTextBox.Selection.Select(lineStart, lineEnd);
            
            }

        }



        private void CopyAllTableButton_Click(object sender, RoutedEventArgs e)
        {
            _filterLogFile.CopyResultsToClipboard();
        }

        private void LogRichTextBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(CalculatorMode_CheckBox.IsChecked == true)
            {

                // Получаем текст выделенной строки
                var lineText = GetSelectedLineText();
                Console.WriteLine(lineText);
                if (string.IsNullOrEmpty(lineText)) return;

                // Извлекаем timestamp из строки
                string timestamp = ExtractTimestamp(lineText);
                Console.WriteLine(timestamp);
                if (string.IsNullOrEmpty(timestamp)) return;

                // Определяем, какой TextBox заполнять (Time1 или Time2)
                if (Time1TextBox.Text == "")
                {
                    Time1TextBox.Text = timestamp;
                    lineText1 = lineText;
                }
                else if (Time2TextBox.Text == "")
                {
                    Time2TextBox.Text = timestamp;
                    lineText2 = lineText;
                    CalculateAndShowResult(lineText1, lineText2);
                    Time1TextBox.Text = "";
                    Time2TextBox.Text = "";
                }
            }
        }

        private void CalculateAndShowResult(string lineText1, string lineText2)
        {
            try
            {
                // 1. Парсим таймштампы
                if (!DateTime.TryParse(Time1TextBox.Text, out DateTime time1) ||
                    !DateTime.TryParse(Time2TextBox.Text, out DateTime time2))
                {
                    MessageBox.Show("Невозможно распознать временные метки!");
                    return;
                }

                // 2. Вычисляем разницу
                TimeSpan difference = time2 - time1; // time2 - time1 (а не наоборот)
                string result = $"{difference.TotalSeconds:0.000} с";

                // 3. Добавляем результат в коллекцию
                _logCalcResults.Add(new LogCalcResult
                {
                    LineText1 = lineText1.Trim(),
                    LineText2 = lineText2.Trim(),
                    Result = result
                });

                // 4. Обновляем DataGrid
                ResultsDataGridCalc.ItemsSource = null;
                ResultsDataGridCalc.ItemsSource = _logCalcResults;
                ResultsDataGridCalc.ScrollIntoView(_logCalcResults.Last());

                // 5. Очищаем поля ПОСЛЕ вычислений
                Time1TextBox.Text = "";
                Time2TextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        // Копирование по кнопке
        private void CopyRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsTab.IsSelected)
            {
                var selectedItem = ResultsDataGrid.SelectedItem as LogResult; // Изменили тип здесь
                if (selectedItem != null)
                {
                    _filterLogFile.CopySingleRowToClipboard(selectedItem);
                    ShowCopyNotification();
                }
            }
            else if (CalculatorTab.IsSelected)
            {
                var selectedItem = ResultsDataGridCalc.SelectedItem as LogCalcResult; // Изменили тип здесь
                if (selectedItem != null)
                {
                    _filterLogFile.CopySingleCalcRowToClipboard(selectedItem);
                    ShowCopyNotification();
                }
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

        public class LogCalcResult
        {
            public string LineText1 { get; set; }
            public string LineText2 { get; set; }
            public string Result { get; set; }
            public string GetCopyText() => $"{LineText1}\n{LineText2}\n{Result}";
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
            try
            {
                // Инициализируем диалог
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Выберите папку с логами",
                    ShowNewFolderButton = false
                };

                // Устанавливаем начальный путь из настроек (если он существует)
                if (!string.IsNullOrEmpty(Properties.Settings.Default.LastOpenedFolder) &&
                    Directory.Exists(Properties.Settings.Default.LastOpenedFolder))
                {
                    dialog.SelectedPath = Properties.Settings.Default.LastOpenedFolder;
                }
                else
                {
                    // Иначе используем стандартную папку "Мои документы"
                    dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                // Показываем диалог
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Сохраняем выбранный путь в настройки
                    Properties.Settings.Default.LastOpenedFolder = dialog.SelectedPath;
                    Properties.Settings.Default.Save();

                    return dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибок
                Console.WriteLine($"Ошибка при выборе папки: {ex.Message}");
                MessageBox.Show("Не удалось открыть диалог выбора папки", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
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

            if (sender == OpenFolderCK11)
            {
                selectedFolder = @"C:\Program Files\Monitel\CK-11\Client\Log";
                AddLogResultInDataGrid("Клиентские логи CK-11");
                if (!Directory.Exists(selectedFolder))
                {
                    MessageBox.Show($"Папка не найдена: {selectedFolder}");
                    return;
                }
            }
            else
            {
                selectedFolder = OpenFolderDialog();
                AddLogResultInDataGrid(selectedFolder);
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    return;
                }
            }

            _currentLogFolderPath = selectedFolder;
            Console.WriteLine(_currentLogFolderPath);



            _logFiles = await _getLogFiles.GetLocalFilesAsync(selectedFolder);
            ApplyFilters();
            string[] archiveExtensions = { ".zip", ".rar", ".7z", ".gz", ".tar.gz" };

            _foundArchives = Directory.GetFiles(selectedFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => archiveExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (_foundArchives.Any())
            {
                ArchivesCountLabel.Text = $"Архивов: {_foundArchives.Count}";
                ArchivesPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ArchivesPanel.Visibility = Visibility.Collapsed;
            }
        }


        private async void ExtractArchives_Click(object sender, RoutedEventArgs e)
        {
            StatusProgressBar.Visibility = Visibility.Visible;
            StatusProgressBar.IsIndeterminate = true; // Бесконечная анимация
            StatusText.Text = "Распаковка архивов...";
            string extractPath = _currentLogFolderPath;
            Directory.CreateDirectory(extractPath);

            foreach (var archive in _foundArchives.ToList()) // ToList() для создания копии коллекции
            {
                try
                {
                    if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        ExtractZip(archive, extractPath);
                    }
                    else if (archive.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                    {
                        ExtractGzFile(archive, extractPath);
                    }
                    else
                    {
                        ExtractWithSharpCompress(archive, extractPath);
                    }

                    // Удаление архива после успешной распаковки
                    File.Delete(archive);
                    _foundArchives.Remove(archive); // Удаляем из коллекции
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при распаковке {Path.GetFileName(archive)}: {ex.Message}");
                }
            }

            // Загружаем файлы уже из распакованной папки
            _currentLogFolderPath = extractPath;
            _logFiles = await _getLogFiles.GetLocalFilesAsync(extractPath);
            ApplyFilters();

            // Прячем панель архивов
            ArchivesPanelVisibility = Visibility.Collapsed;
            StatusProgressBar.Visibility = Visibility.Collapsed;
            StatusProgressBar.IsIndeterminate = false;
            MessageBox.Show("Архивы успешно распакованы, исходные архивы удалены.");
        }


        // ZIP
        private void ExtractZip(string zipPath, string extractPath)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("/"))
                    {
                        string destinationPath = Path.Combine(extractPath, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                }
            }
        }

        // GZ
        private void ExtractGzFile(string gzFilePath, string outputDir)
        {
            string fileName = Path.GetFileNameWithoutExtension(gzFilePath);
            string outputFilePath = Path.Combine(outputDir, fileName);

            if (File.Exists(outputFilePath))
                File.Delete(outputFilePath);

            using (FileStream originalFileStream = File.OpenRead(gzFilePath))
            using (FileStream decompressedFileStream = File.Create(outputFilePath))
            using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(decompressedFileStream);
            }
        }

        // RAR, 7Z, TAR.GZ
        private void ExtractWithSharpCompress(string archivePath, string extractPath)
        {
            //using (var archive = ArchiveFactory.Open(archivePath))
            //{
            //    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            //    {
            //        entry.WriteToDirectory(extractPath, new ExtractionOptions()
            //        {
            //            ExtractFullPath = true,
            //            Overwrite = true
            //        });
            //    }
            //}
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

        private async Task<string> ReadLogFileContentAsync()
        {
            if (string.IsNullOrEmpty(_currentLogFilePath))
                return null;

            if (_sshClient != null && _sshClient.IsConnected)
            {
                // Чтение файла через SSH (для Linux-сервера)
                return await ReadFileViaSsh(_currentLogFilePath);
            }
            else
            {
                // Чтение локального файла
                Console.WriteLine(_currentLogFilePath);
                return await ReadLocalFile(_currentLogFilePath);
            }
        }
        public bool IsCalculatorMode()
        {
            return CalculatorMode_CheckBox?.IsChecked == true;
        }

        private async void StartSearchInFilesButton_Left_Click(object sender, RoutedEventArgs e)
        {
            await StartSearchAsync(isLeftFilter: true, IsCalculatorMode());
        }

        private async void StartSearchInFilesButton_Right_Click(object sender, RoutedEventArgs e)
        {
            await StartSearchAsync(isLeftFilter: false, IsCalculatorMode());
        }

        private async Task StartSearchAsync(bool isLeftFilter, bool IsCalculatorMode)
        {
            _counterHelper.ClearLeftCounter();
            _counterHelper.ClearMainCounter();

            if (_isReadingLogs)
                return;

            _isReadingLogs = true;

            try
            {
                string content = await ReadLogFileContentAsync();

                if (content == null)
                    return;

                _logFileService.ApplyLogFilters(content, LogRichTextBox, isLeftFilter, IsCalculatorMode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось прочитать файл логов: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
            finally
            {
                _isReadingLogs = false;
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
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxWidth = 350,
                MaxHeight = 600,
                Style = (Style)FindResource("ModernFlowDocumentViewer")
            };

            // Создаем кнопку "Копировать статистику"
            Button copyButton = new Button
            {
                Content = "Статистика",
                Margin = new Thickness(5),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)FindResource("ModernButton")
            };

            // Создаем кнопку "Копировать в конфиг"
            Button copyConfigButton = new Button
            {
                Content = "Конфиг",
                Margin = new Thickness(5),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)FindResource("ModernButton")
            };

            // Создаем кнопку "Результат"
            Button copyResultButton = new Button
            {
                Content = "в Excel",
                Margin = new Thickness(5),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)FindResource("ModernButton")
            };

            Button copyValuesButton = new Button
            {
                Content = "Значения",
                Margin = new Thickness(5),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)FindResource("ModernButton")
            };

            // Обработчик нажатия на кнопку "Копировать статистику"
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
                    //MessageBox.Show("Статистика скопирована в буфер обмена!", "Успех",
                        //MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка копирования: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Обработчик нажатия на кнопку "Копировать в конфиг"
            copyConfigButton.Click += (s, args) =>
            {
                try
                {
                    // Генерируем JSON конфиг на основе статистики
                    StringBuilder configBuilder = new StringBuilder();
                    configBuilder.AppendLine("{");
                    configBuilder.AppendLine("    \"Endpoint\": \"records/changeRecord\",");
                    configBuilder.AppendLine("    \"JsonTemplate\": \"CreateNewRecord.json\",");
                    configBuilder.AppendLine("    \"Schedule\": {");
                    configBuilder.AppendLine("        \"Days\": 3,");
                    configBuilder.AppendLine("        \"DefaultRate\": 1,");
                    configBuilder.AppendLine("        \"DefaultIntervalMs\": 1000,");
                    configBuilder.AppendLine("        \"HourlyConfigs\": {");

                    // Добавляем конфигурацию для каждого часа
                    for (int hour = 0; hour < 24; hour++)
                    {
                        int count = hourlyStats[hour];

                        // Форматируем строку для каждого часа
                        string hourConfig = $"            \"{hour}\": {{\n                \"Count\": {count}\n            }}";

                        // Добавляем запятую для всех элементов, кроме последнего
                        if (hour < 23)
                        {
                            hourConfig += ",";
                        }

                        configBuilder.AppendLine(hourConfig);
                    }

                    configBuilder.AppendLine("        }");
                    configBuilder.AppendLine("    }");
                    configBuilder.AppendLine("}");

                    string configText = configBuilder.ToString();

                    // Копируем конфиг в буфер обмена
                    Clipboard.SetText(configText);
                    //MessageBox.Show("Конфиг скопирован в буфер обмена!", "Успех",
                    //    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка генерации конфига: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Обработчик нажатия на кнопку "Excel"
            copyResultButton.Click += (s, args) =>
            {
                StringBuilder excelData = new StringBuilder();

                // Добавляем заголовки (опционально)
                excelData.AppendLine("Время\tКоличество");

                for (int hour = 0; hour < 24; hour++)
                {
                    int count = hourlyStats[hour];
                    string hourStr = hour.ToString("00");

                    // Используем табуляцию как разделитель
                    excelData.AppendLine($"{hourStr}:00\t{count}");
                }

                try
                {
                    Clipboard.SetText(excelData.ToString());
                    //MessageBox.Show("Данные скопированы!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            };

            // Обработчик нажатия на кнопку "Значения"
            copyValuesButton.Click += (s, args) =>
            {
                StringBuilder excelData = new StringBuilder();

                // Добавляем заголовки (опционально)
                excelData.AppendLine("Количество");

                for (int hour = 0; hour < 24; hour++)
                {
                    int count = hourlyStats[hour];
                    string hourStr = hour.ToString("00");

                    // Используем табуляцию как разделитель
                    excelData.AppendLine($"{count}");
                }

                try
                {
                    Clipboard.SetText(excelData.ToString());
                    //MessageBox.Show("Данные скопированы!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            };

            // Создаем контейнер для кнопок
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(5)
            };

            buttonPanel.Children.Add(copyConfigButton);
            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(copyResultButton);
            buttonPanel.Children.Add(copyValuesButton);

            // Создаем основной контейнер
            StackPanel mainPanel = new StackPanel();
            mainPanel.Children.Add(documentViewer);
            mainPanel.Children.Add(buttonPanel);

            // Создаем окно для отображения статистики
            Window statsWindow = new Window
            {
                Title = "Статистика по часам",
                Content = mainPanel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };

            statsWindow.Show();
        }

        private void OpenSSHConnectionWindow_Click(object sender, RoutedEventArgs e)
        {
            var sshWindow = new SSHConnectionWindow();
            sshWindow.Owner = this;
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
                    Filter_One = ConfigSearch.String1LeftSearchText?.ToString(),
                    Filter_Two = ConfigSearch.String2LeftSearchText?.ToString(),
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
            AddLogResultInDataGrid(_currentLogFilePath);
        }

        private void CalculatorMode_CheckBox_Unhecked(object sender, RoutedEventArgs e)
        {
            LeftBorder.Background = Brushes.White;
            LeftBorder.BorderThickness = new Thickness(1);
            RightBorder.Background = Brushes.White;
            RightBorder.BorderThickness = new Thickness(1);
            ResultsTab.IsSelected = true;
        }

        private void ClearResultsButton_Click(object sender, RoutedEventArgs e)
        {
            _filterLogFile.ClearResultsButton_Click();
        }

        private void OpenSharedFoldersWindow_Click(object sender, RoutedEventArgs e)
        {
            var sharedFoldersWindow = new SharedFoldersWindow();
            sharedFoldersWindow.Show();
        }

        private void OpenSSHConnectionWindow_Click(object sender, object e)
        {
           
        }

        private void OpenFolder_Click(object sender, object e)
        {

        }

        private void MenuItem_OpenNotepad_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                // Пытаемся открыть Notepad++
                Process.Start("notepad++.exe", "-n");
            }
            catch (Exception)
            {
                try
                {
                    // Если Notepad++ не найден, открываем стандартный блокнот
                    Process.Start("notepad.exe");
                }
                catch (Exception ex)
                {
                    // Если и блокнот не открывается, показываем сообщение об ошибке
                    MessageBox.Show($"Не удалось открыть редактор: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SaveFileToLocal_Click(object sender, RoutedEventArgs e)
        {
            if (!(LogList.SelectedItem is string selectedFileName) || _sshClient == null || !_sshClient.IsConnected)
                return;

            try
            {
                // Находим полный путь к файлу на сервере
                var fullServerPath = _logFiles.FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(selectedFileName, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(fullServerPath))
                {
                    MessageBox.Show("Файл не найден на сервере");
                    return;
                }

                // Диалог для выбора места сохранения
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = selectedFileName,
                    Filter = "Все файлы|*.*",
                    Title = "Сохранить файл на локальный компьютер"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    StatusText.Text = "Скачивание файла...";

                    // Скачиваем файл через SCP
                    await DownloadFileViaScp(fullServerPath, saveDialog.FileName);

                    StatusText.Text = $"Файл сохранен: {saveDialog.FileName}";
                    MessageBox.Show($"Файл успешно сохранен:\n{saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}");
            }
        }

        private async Task DownloadFileViaScp(string remoteFilePath, string localFilePath)
        {
            using (var scpClient = new ScpClient(_sshClient.ConnectionInfo))
            {
                await Task.Run(() => scpClient.Connect());

                if (scpClient.IsConnected)
                {
                    // Скачиваем файл
                    await Task.Run(() => scpClient.Download(remoteFilePath, new FileInfo(localFilePath)));
                }
                else
                {
                    throw new Exception("Не удалось подключиться для скачивания файла");
                }
            }
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (!(LogList.SelectedItem is string selectedFileName))
                return;

            var fullPath = Path.Combine(_currentLogFolderPath, selectedFileName);

            if (File.Exists(fullPath))
            {
                // Открываем папку и выделяем файл в проводнике
                Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
            }
            else
            {
                MessageBox.Show("Файл не найден");
            }
        }
        private void AddString1(object parameter)
        {
            if (parameter is string newItem)
            {
                _configSearchVM.AddString1Item(newItem);
            }
        }

        private void AddString2(object parameter)
        {
            if (parameter is string newItem)
            {
                _configSearchVM.AddString2Item(newItem);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }

}