// FilterLogFile.cs
using GetStatistics.Models;
using GetStatistics;
using System.Text;
using System;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows;

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
        AddToResultsDataGrid(filters, _counter, 100);
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

            // Цвет для выделения совпадений
            var highlightBrush = isLeftFilter ? Brushes.Yellow: Brushes.LightBlue;
            var activeFilters = GetActiveFilters(filters);

            foreach (var line in lines)
            {
                var text = line + "\n";

                if (activeFilters.Count == 0)
                {
                    paragraph.Inlines.Add(new Run(text));
                }
                else
                {
                    var span = new Span();
                    FindAndHighlightMatches(span, text, activeFilters, highlightBrush);
                    paragraph.Inlines.Add(span);
                }
            }

            _logRichTextBox.Document.Blocks.Clear();
            _logRichTextBox.Document.Blocks.Add(paragraph);
        });
    }

    private void FindAndHighlightMatches(Span container, string text, List<string> filters, Brush highlightBrush)
    {
        var matches = new List<TextMatch>();

        // Находим все совпадения для всех фильтров
        foreach (var filter in filters)
        {
            if (string.IsNullOrEmpty(filter)) continue;

            int index = 0;
            while ((index = text.IndexOf(filter, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                matches.Add(new TextMatch(index, filter.Length));
                index += filter.Length;
            }
        }

        // Если нет совпадений - просто добавляем текст
        if (matches.Count == 0)
        {
            container.Inlines.Add(new Run(text));
            return;
        }

        // Сортируем совпадения по позиции
        matches.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Строим текст с выделениями
        int currentPos = 0;
        foreach (var match in matches)
        {
            // Текст до совпадения
            if (match.Start > currentPos)
            {
                container.Inlines.Add(new Run(text.Substring(currentPos, match.Start - currentPos)));
            }

            // Выделенное совпадение
            container.Inlines.Add(new Run(text.Substring(match.Start, match.Length))
            {
                Background = highlightBrush,
                //FontWeight = FontWeights.Bold
            });

            currentPos = match.Start + match.Length;
        }

        // Остаток текста после последнего совпадения
        if (currentPos < text.Length)
        {
            container.Inlines.Add(new Run(text.Substring(currentPos)));
        }
    }

    // Вспомогательная структура для хранения информации о совпадении
    private readonly struct TextMatch
    {
        public TextMatch(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int Start { get; }
        public int Length { get; }
    }

    private List<string> GetActiveFilters(FilterParameters filters)
    {
        var activeFilters = new List<string>();

        if (!string.IsNullOrEmpty(filters.Filter_One))
            activeFilters.Add(filters.Filter_One);
        if (!string.IsNullOrEmpty(filters.Filter_Two))
            activeFilters.Add(filters.Filter_Two);
        if (!string.IsNullOrEmpty(filters.SearchText_One))
            activeFilters.Add(filters.SearchText_One);
        if (!string.IsNullOrEmpty(filters.SearchText_Two))
            activeFilters.Add(filters.SearchText_Two);

        return activeFilters;
    }

    public class FilterResultItem
    {
        public string Filters { get; set; }
        public int Counter { get; set; }
        public int FullCounter { get; set; }
    }

    public void AddToResultsDataGrid(FilterParameters filters, int counter, int fullCounter)
    {
        if (_mainWindow.ResultsDataGrid == null || !_mainWindow.ResultsDataGrid.CheckAccess())
            return;

        _mainWindow.ResultsDataGrid.Dispatcher.Invoke(() =>
        {
            // Создаем строку для DataGrid
            var newItem = new FilterResultItem
            {
                Filters = FormatFilters(filters),
                Counter = counter,
                FullCounter = fullCounter
            };

            // Добавляем новую строку в DataGrid
            if (_mainWindow.ResultsDataGrid.ItemsSource == null)
            {
                _mainWindow.ResultsDataGrid.ItemsSource = new List<FilterResultItem> { newItem };
            }
            else if (_mainWindow.ResultsDataGrid.ItemsSource is IList<FilterResultItem> itemsList)
            {
                itemsList.Add(newItem);
                _mainWindow.ResultsDataGrid.Items.Refresh();
            }
        });
    }

    private string FormatFilters(FilterParameters filters)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(filters.Filter_One))
            sb.Append($"1: {filters.Filter_One} ");

        if (!string.IsNullOrEmpty(filters.Filter_Two))
            sb.Append($"2: {filters.Filter_Two} ");

        if (!string.IsNullOrEmpty(filters.SearchText_One))
            sb.Append($"S1: {filters.SearchText_One} ");

        if (!string.IsNullOrEmpty(filters.SearchText_Two))
            sb.Append($"S2: {filters.SearchText_Two}");

        return sb.ToString().Trim();
    }
}