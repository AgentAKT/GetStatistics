using System;
using System.Collections.Generic;
using System.IO;  // Необходимо для File.ReadAllLines
using System.Linq;

public class QuoteManager
{
    private readonly List<(string quote, string author)> _quotes = new List<(string, string)>();

    public QuoteManager(string quotesFilePath)
    {
        LoadQuotes(quotesFilePath);
    }

    private void LoadQuotes(string filePath)
    {
        try
        {
            // Проверяем существование файла перед чтением
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException();
            }

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var parts = line.Split('|');
                if (parts.Length >= 2)
                {
                    _quotes.Add((parts[0].Trim(), parts[1].Trim()));
                }
                else if (parts.Length == 1)
                {
                    _quotes.Add((parts[0].Trim(), "Неизвестный автор"));
                }
            }
        }





        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            // Дефолтные цитаты, если файл не найден
            _quotes.AddRange(new[]
            {
                ("Программирование — это искусство создавать решения из ничего.", "Неизвестный автор"),
                ("Код должен быть написан так, чтобы его мог понять даже новичок.", "Robert C. Martin"),
                ("Лучший код — это отсутствие кода.", "Jeff Atwood")
            });
        }
        catch (Exception ex)
        {
            _quotes.Add(($"Ошибка загрузки цитат: {ex.Message}", "Система"));
        }
    }

    public (string quote, string author) GetRandomQuote()
    {
        if (_quotes.Count == 0)
            return ("Нет доступных цитат", "");

        var random = new Random();
        return _quotes[random.Next(_quotes.Count)];
    }
}