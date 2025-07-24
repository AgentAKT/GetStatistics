using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

public class ConfigLoader
{
    private readonly ComboBox _stringComboBox_One_Left;
    private readonly ComboBox _stringComboBox_Two_Left;
    private readonly ComboBox _stringComboBox_One_Right;
    private readonly ComboBox _stringComboBox_Two_Right;

    private Config _config;

    public ConfigLoader(
        ComboBox StringComboBox_One_Left,
        ComboBox StringComboBox_Two_Left,
        ComboBox StringComboBox_One_Right,
        ComboBox StringComboBox_Two_Right)
    {
        _stringComboBox_One_Left = StringComboBox_One_Left;
        _stringComboBox_Two_Left = StringComboBox_Two_Left;
        _stringComboBox_One_Right = StringComboBox_One_Right;
        _stringComboBox_Two_Right = StringComboBox_Two_Right;
    }

    public async Task LoadConfigAsync(string configPath)
    {
        try
        {
            _config = await Task.Run(() => Config.Load(configPath));

            // Заполняем String1 и String2 сразу (если не зависят от папки)
            _stringComboBox_One_Left.ItemsSource = _config.String1 ?? new List<string>();
            _stringComboBox_Two_Left.ItemsSource = _config.String2 ?? new List<string>();
            _stringComboBox_One_Right.ItemsSource = _config.String1 ?? new List<string>();
            _stringComboBox_Two_Right.ItemsSource = _config.String2 ?? new List<string>();

        }
        catch (Exception ex)
        {
            //MessageBox.Show($"Ошибка загрузки конфигурации: {ex.Message}");
        }
    }
}