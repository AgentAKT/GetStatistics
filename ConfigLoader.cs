using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;

public class ConfigLoader
{
    private readonly TextBox _textBox1Left;
    private readonly TextBox _textBox2Left;
    private readonly TextBox _textBox1Right;
    private readonly TextBox _textBox2Right;

    public ConfigLoader(TextBox textBox1Left, TextBox textBox2Left,
                       TextBox textBox1Right, TextBox textBox2Right)
    {
        _textBox1Left = textBox1Left;
        _textBox2Left = textBox2Left;
        _textBox1Right = textBox1Right;
        _textBox2Right = textBox2Right;
    }

    public async Task LoadConfigAsync(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                return;

            // Используем синхронное чтение, так как File.ReadAllTextAsync не существует
            string json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<ConfigData>(json);

            if (config != null)
            {
                // Устанавливаем подсказки или начальные значения
                // Не присваиваем List<string> напрямую TextBox.Text
                _textBox1Left.Text = config.String1?.Count > 0 ? config.String1[0] : "";
                _textBox2Left.Text = config.String2?.Count > 0 ? config.String2[0] : "";
                _textBox1Right.Text = config.String1?.Count > 0 ? config.String1[0] : "";
                _textBox2Right.Text = config.String2?.Count > 0 ? config.String2[0] : "";

                // Или устанавливаем ToolTip с доступными значениями
                _textBox1Left.ToolTip = $"Доступные значения: {string.Join(", ", config.String1 ?? new List<string>())}";
                _textBox2Left.ToolTip = $"Доступные значения: {string.Join(", ", config.String2 ?? new List<string>())}";
                _textBox1Right.ToolTip = $"Доступные значения: {string.Join(", ", config.String1 ?? new List<string>())}";
                _textBox2Right.ToolTip = $"Доступные значения: {string.Join(", ", config.String2 ?? new List<string>())}";
            }
        }
        catch
        {
            // Игнорируем ошибки загрузки конфига
        }
    }
}

public class ConfigData
{
    public List<string> String1 { get; set; }
    public List<string> String2 { get; set; }
}