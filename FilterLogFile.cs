// FilterLogFile.cs
using GetStatistics.Models;
using GetStatistics;
using System.Text;
using System;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

internal class FilterLogFile
{
    private readonly MainWindow _mainWindow;
    private readonly RichTextBox _logRichTextBox;
    private string _logContent;
    private int _counter;

    public FilterLogFile(RichTextBox targetTextBox, MainWindow mainWindow)
    {
        _logRichTextBox = targetTextBox;
        _mainWindow = mainWindow;
    }

    public void SetContent(string logContent)
    {
        _logContent = logContent;
    }

    public void ApplyLogFilters(FilterParameters filters, bool isLeftFilter)
    {
        if (string.IsNullOrEmpty(_logContent))
            return;

        var filteredContent = FilterContent(filters, isLeftFilter);
        UpdateRichTextBox(filteredContent, filters, isLeftFilter);
    }

    private string FilterContent(FilterParameters filters, bool isLeftFilter)
    {
        _counter = 0;
        var lines = _logContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            bool matches = MatchFilters(line, filters);

            if (matches)
            {
                result.AppendLine(line);
                _counter++;
            }
        }

        _mainWindow.UpdateStatusText(_counter.ToString());
        return result.ToString();
    }

    private bool MatchFilters(string line, FilterParameters filters)
    {
        bool matches = true;

        // Проверяем все заполненные фильтры
        if (!string.IsNullOrEmpty(filters.Filter_One))
            matches &= line.IndexOf(filters.Filter_One, StringComparison.OrdinalIgnoreCase) >= 0;

        if (!string.IsNullOrEmpty(filters.Filter_Two))
            matches &= line.IndexOf(filters.Filter_Two, StringComparison.OrdinalIgnoreCase) >= 0;

        if (!string.IsNullOrEmpty(filters.SearchText_One))
            matches &= line.IndexOf(filters.SearchText_One, StringComparison.OrdinalIgnoreCase) >= 0;

        if (!string.IsNullOrEmpty(filters.SearchText_Two))
            matches &= line.IndexOf(filters.SearchText_Two, StringComparison.OrdinalIgnoreCase) >= 0;

        return matches;
    }

    private void UpdateRichTextBox(string logContent, FilterParameters filters, bool isLeftFilter)
    {
        if (_logRichTextBox == null || !_logRichTextBox.CheckAccess())
            return;

        _logRichTextBox.Dispatcher.Invoke(() =>
        {
            var paragraph = new Paragraph();
            var lines = logContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var run = new Run(line + "\n");

                // Подсвечиваем в зависимости от типа фильтра
                run.Background = isLeftFilter ? Brushes.Yellow : Brushes.LightBlue;
                paragraph.Inlines.Add(run);
            }

            _logRichTextBox.Document.Blocks.Clear();
            _logRichTextBox.Document.Blocks.Add(paragraph);
        });
    }
}