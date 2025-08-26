using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

public class ConfigSearchViewModel : INotifyPropertyChanged
{
    private ConfigData _configData;
    private string _configFilePath;

    // Для всех 4 фильтров
    private string _string1LeftSearchText;
    private string _string2LeftSearchText;
    private string _string1RightSearchText;
    private string _string2RightSearchText;

    private string _selectedString1Left;
    private string _selectedString2Left;
    private string _selectedString1Right;
    private string _selectedString2Right;

    public event PropertyChangedEventHandler PropertyChanged;

    // Коллекции для всех 4 фильтров
    public ObservableCollection<string> FilteredString1LeftItems { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> FilteredString2LeftItems { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> FilteredString1RightItems { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> FilteredString2RightItems { get; } = new ObservableCollection<string>();

    // Свойства для поисковых текстов
    public string String1LeftSearchText
    {
        get => _string1LeftSearchText;
        set
        {
            if (_string1LeftSearchText != value)
            {
                _string1LeftSearchText = value;
                OnPropertyChanged();
                FilterItems(FilterType.String1Left);
            }
        }
    }

    public string String2LeftSearchText
    {
        get => _string2LeftSearchText;
        set
        {
            if (_string2LeftSearchText != value)
            {
                _string2LeftSearchText = value;
                OnPropertyChanged();
                FilterItems(FilterType.String2Left);
            }
        }
    }

    public string String1RightSearchText
    {
        get => _string1RightSearchText;
        set
        {
            if (_string1RightSearchText != value)
            {
                _string1RightSearchText = value;
                OnPropertyChanged();
                FilterItems(FilterType.String1Right);
            }
        }
    }

    public string String2RightSearchText
    {
        get => _string2RightSearchText;
        set
        {
            if (_string2RightSearchText != value)
            {
                _string2RightSearchText = value;
                OnPropertyChanged();
                FilterItems(FilterType.String2Right);
            }
        }
    }

    // Свойства для выбранных элементов
    public string SelectedString1Left
    {
        get => _selectedString1Left;
        set
        {
            if (_selectedString1Left != value)
            {
                _selectedString1Left = value;
                OnPropertyChanged();

                if (value != null)
                {
                    String1LeftSearchText = value;
                    FilteredString1LeftItems.Clear();
                }
            }
        }
    }

    public string SelectedString2Left
    {
        get => _selectedString2Left;
        set
        {
            if (_selectedString2Left != value)
            {
                _selectedString2Left = value;
                OnPropertyChanged();

                if (value != null)
                {
                    String2LeftSearchText = value;
                    FilteredString2LeftItems.Clear();
                }
            }
        }
    }

    public string SelectedString1Right
    {
        get => _selectedString1Right;
        set
        {
            if (_selectedString1Right != value)
            {
                _selectedString1Right = value;
                OnPropertyChanged();

                if (value != null)
                {
                    String1RightSearchText = value;
                    FilteredString1RightItems.Clear();
                }
            }
        }
    }

    public string SelectedString2Right
    {
        get => _selectedString2Right;
        set
        {
            if (_selectedString2Right != value)
            {
                _selectedString2Right = value;
                OnPropertyChanged();

                if (value != null)
                {
                    String2RightSearchText = value;
                    FilteredString2RightItems.Clear();
                }
            }
        }
    }

    public ConfigSearchViewModel()
    {
        _configFilePath = GetConfigFilePath();
        LoadConfig();
    }

    private string GetConfigFilePath()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataPath, "LogAnalyzer");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "config.json");
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                _configData = JsonConvert.DeserializeObject<ConfigData>(json);
            }
            else
            {
                _configData = CreateDefaultConfig();
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки конфига: {ex.Message}");
            _configData = CreateDefaultConfig();
        }
    }

    private ConfigData CreateDefaultConfig()
    {
        return new ConfigData
        {
            String1 = new List<string>
            {
                "http", "Запрос опции", "Master", "Делаю мастером командой оператора",
                "WRN", "ERR", "INF", "DBG", "exceptions", "errors", "libVersion"
            },
            String2 = new List<string>
            {
                "AddressSearch", "AlarmHostingService", "CIMAccess", "CIMExport", "CIMImport",
                "CurrentMonitoring", "Defects", "DispJournal", "DocumentsCDS", "DynamicLimits",
                "EqVoltageMonitoring", "EventNotifier", "GeoData", "GeoModel", "GeoOms",
                "JaiService", "JaiSync", "MaiService", "Marks", "MdsManager",
                // ... остальные значения
            }
        };
    }

    private void SaveConfig()
    {
        try
        {
            string json = JsonConvert.SerializeObject(_configData, Formatting.Indented);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения конфига: {ex.Message}");
        }
    }

    private void FilterItems(FilterType filterType)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ObservableCollection<string> filteredItems;
            List<string> sourceItems;
            string searchText;

            switch (filterType)
            {
                case FilterType.String1Left:
                    filteredItems = FilteredString1LeftItems;
                    sourceItems = _configData.String1;
                    searchText = String1LeftSearchText;
                    break;
                case FilterType.String2Left:
                    filteredItems = FilteredString2LeftItems;
                    sourceItems = _configData.String2;
                    searchText = String2LeftSearchText;
                    break;
                case FilterType.String1Right:
                    filteredItems = FilteredString1RightItems;
                    sourceItems = _configData.String1;
                    searchText = String1RightSearchText;
                    break;
                case FilterType.String2Right:
                    filteredItems = FilteredString2RightItems;
                    sourceItems = _configData.String2;
                    searchText = String2RightSearchText;
                    break;
                default:
                    return;
            }

            filteredItems.Clear();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var item in sourceItems.Take(5))
                    filteredItems.Add(item);
                return;
            }

            var filtered = sourceItems
                .Where(item => item?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(10);

            foreach (var item in filtered)
            {
                filteredItems.Add(item);
            }
        });
    }

    public void AddString1Item(string newItem, bool isLeftSide = true)
    {
        if (!string.IsNullOrWhiteSpace(newItem) && !_configData.String1.Contains(newItem))
        {
            _configData.String1.Add(newItem);
            SaveConfig();
            FilterItems(isLeftSide ? FilterType.String1Left : FilterType.String1Right);
        }
    }

    public void AddString2Item(string newItem, bool isLeftSide = true)
    {
        if (!string.IsNullOrWhiteSpace(newItem) && !_configData.String2.Contains(newItem))
        {
            _configData.String2.Add(newItem);
            SaveConfig();
            FilterItems(isLeftSide ? FilterType.String2Left : FilterType.String2Right);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum FilterType
{
    String1Left,
    String2Left,
    String1Right,
    String2Right
}
