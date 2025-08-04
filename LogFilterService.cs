using GetStatistics.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace GetStatistics.Services
{
    public class LogFilterService
    {
        private readonly MainWindow _mainWindow;
        private readonly RichTextBox _logRichTextBox;
        private string _logContent;
        private int _counter;

        public LogFilterService(RichTextBox targetTextBox, MainWindow mainWindow)
        {
            _logRichTextBox = targetTextBox ?? throw new ArgumentNullException(nameof(targetTextBox));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public void SetContent(string logContent)
        {
            _logContent = logContent ?? string.Empty;
        }

        public void ApplyFilters(FilterParameters filters, bool isLeftFilter)
        {
            if (string.IsNullOrEmpty(_logContent)) return;

            var filteredContent = FilterContent(filters);
            UpdateRichTextBox(filteredContent, filters, isLeftFilter);
            AddToResultsGrid(filters, _counter, _counter);
        }

        private string FilterContent(FilterParameters filters)
        {
            _counter = 0;
            var lines = _logContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();

            foreach (var line in lines)
            {
                if (MatchFilters(line, filters))
                {
                    result.AppendLine(line);
                    _counter++;
                }
            }

            UpdateStatusCounter(_counter);
            return result.ToString();
        }

        private bool MatchFilters(string line, FilterParameters filters)
        {
            return CheckFilter(line, filters.Filter_One) &&
                   CheckFilter(line, filters.Filter_Two) &&
                   CheckFilter(line, filters.SearchText_One) &&
                   CheckFilter(line, filters.SearchText_Two);
        }

        private bool CheckFilter(string line, string filter)
        {
            return string.IsNullOrEmpty(filter) ||
                   line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateStatusCounter(int count)
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.UpdateStatusText(count.ToString());
                _mainWindow.StringCounter_Main.Content = count.ToString();
            });
        }

        private void UpdateRichTextBox(string content, FilterParameters filters, bool isLeftFilter)
        {
            if (_logRichTextBox == null) return;

            _logRichTextBox.Dispatcher.Invoke(() =>
            {
                var paragraph = new Paragraph();
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var highlightBrush = isLeftFilter ? Brushes.Yellow : Brushes.LightBlue;
                var activeFilters = GetActiveFilters(filters);

                if (_mainWindow.Unical_CheckBox.IsChecked == true)
                {
                    ProcessUniqueLines(lines, paragraph, activeFilters, highlightBrush, filters);
                }
                else
                {
                    ProcessAllLines(lines, paragraph, activeFilters, highlightBrush);
                }

                _logRichTextBox.Document.Blocks.Clear();
                _logRichTextBox.Document.Blocks.Add(paragraph);
            });
        }

        private void ProcessUniqueLines(string[] lines, Paragraph paragraph,
            List<string> filters, Brush highlightBrush, FilterParameters parameters)
        {
            var uniqueMatches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _counter = 0;

            foreach (var line in lines)
            {
                if (!ContainsAllFilters(line, filters, parameters)) continue;

                var matchedText = ExtractMatchedText(line, parameters);
                if (matchedText == null || uniqueMatches.ContainsKey(matchedText)) continue;

                uniqueMatches[matchedText] = line + "\n";
                _counter++;
                AddHighlightedText(paragraph, line + "\n", filters, highlightBrush);
            }

            UpdateStatusCounter(_counter);
        }

        private bool ContainsAllFilters(string line, List<string> filters, FilterParameters parameters)
        {
            return filters.All(filter =>
                filter == parameters.SearchText_One ||
                filter == parameters.SearchText_Two ||
                line.Contains(filter));
        }

        private string ExtractMatchedText(string line, FilterParameters parameters)
        {
            if (string.IsNullOrEmpty(parameters.SearchText_One) ||
                string.IsNullOrEmpty(parameters.SearchText_Two))
                return null;

            int start = line.IndexOf(parameters.SearchText_One);
            if (start == -1) return null;

            start += parameters.SearchText_One.Length;
            int end = line.IndexOf(parameters.SearchText_Two, start);
            if (end == -1) return null;

            return line.Substring(start, end - start).Trim();
        }

        private void ProcessAllLines(string[] lines, Paragraph paragraph,
            List<string> filters, Brush highlightBrush)
        {
            foreach (var line in lines)
            {
                AddHighlightedText(paragraph, line + "\n", filters, highlightBrush);
            }
        }

        private void AddHighlightedText(Paragraph paragraph, string text,
            List<string> filters, Brush highlightBrush)
        {
            var span = new Span();

            if (filters.Count > 0)
            {
                HighlightMatches(span, text, filters, highlightBrush);
            }
            else
            {
                span.Inlines.Add(new Run(text));
            }

            paragraph.Inlines.Add(span);
        }

        private void HighlightMatches(Span container, string text,
            List<string> filters, Brush highlightBrush)
        {
            var matches = FindAllMatches(text, filters);

            if (matches.Count == 0)
            {
                container.Inlines.Add(new Run(text));
                return;
            }

            BuildHighlightedText(container, text, matches, highlightBrush);
        }

        private List<TextMatch> FindAllMatches(string text, List<string> filters)
        {
            var matches = new List<TextMatch>();

            foreach (var filter in filters.Where(f => !string.IsNullOrEmpty(f)))
            {
                int index = 0;
                while ((index = text.IndexOf(filter, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    matches.Add(new TextMatch(index, filter.Length));
                    index += filter.Length;
                }
            }

            matches.Sort((a, b) => a.Start.CompareTo(b.Start));
            return matches;
        }

        private void BuildHighlightedText(Span container, string text,
            List<TextMatch> matches, Brush highlightBrush)
        {
            int currentPos = 0;

            foreach (var match in matches)
            {
                if (match.Start > currentPos)
                {
                    container.Inlines.Add(new Run(text.Substring(currentPos, match.Start - currentPos)));
                }

                container.Inlines.Add(new Run(text.Substring(match.Start, match.Length))
                {
                    Background = highlightBrush
                });

                currentPos = match.Start + match.Length;
            }

            if (currentPos < text.Length)
            {
                container.Inlines.Add(new Run(text.Substring(currentPos)));
            }
        }

        private List<string> GetActiveFilters(FilterParameters filters)
        {
            return new List<string>
            {
                filters.Filter_One,
                filters.Filter_Two,
                filters.SearchText_One,
                filters.SearchText_Two
            }.Where(f => !string.IsNullOrEmpty(f)).ToList();
        }

        public void AddToResultsGrid(FilterParameters filters, int counter, int fullCounter)
        {
            _mainWindow.ResultsDataGrid?.Dispatcher.Invoke(() =>
            {
                var newItem = new FilterResultItem
                {
                    Filters = FormatFilters(filters),
                    Counter = counter,
                    FullCounter = fullCounter
                };

                if (_mainWindow.ResultsDataGrid.ItemsSource is IList<FilterResultItem> itemsList)
                {
                    itemsList.Add(newItem);
                    _mainWindow.ResultsDataGrid.Items.Refresh();
                }
            });
        }

        private string FormatFilters(FilterParameters filters)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(filters.Filter_One))
                parts.Add($"1: {filters.Filter_One}");

            if (!string.IsNullOrEmpty(filters.Filter_Two))
                parts.Add($"2: {filters.Filter_Two}");

            if (!string.IsNullOrEmpty(filters.SearchText_One))
                parts.Add($"S1: {filters.SearchText_One}");

            if (!string.IsNullOrEmpty(filters.SearchText_Two))
                parts.Add($"S2: {filters.SearchText_Two}");

            return string.Join(" \n", parts);
        }

        private readonly struct TextMatch
        {
            public int Start { get; }
            public int Length { get; }

            public TextMatch(int start, int length)
            {
                Start = start;
                Length = length;
            }
        }
    }

    public class FilterResultItem
    {
        public string Filters { get; set; }
        public int Counter { get; set; }
        public int FullCounter { get; set; }
    }
}