using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ConfigService
{
    private const string ConfigFileName = "ssh-connections.json";
    private readonly string _configFilePath;

    public ConfigService()
    {
        // Храним конфиг в папке AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "GetStatistics");
        Directory.CreateDirectory(appFolder);
        _configFilePath = Path.Combine(appFolder, ConfigFileName);

        Console.WriteLine($"Config file path: {_configFilePath}");
    }

    public AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                Console.WriteLine($"Config file exists. Loading...");
                var json = File.ReadAllText(_configFilePath);
                Console.WriteLine($"Config file content: {json}");

                var config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                Console.WriteLine($"Loaded {config.SshConnections?.Count ?? 0} connections");
                return config;
            }
            else
            {
                Console.WriteLine($"Config file does not exist. Creating default config.");
                // Создаем файл с default конфигом
                var defaultConfig = new AppConfig
                {
                    SshConnections = new List<SshConnectionConfig>
                    {
                        new SshConnectionConfig
                        {
                            Name = "UAT.ZES.SCADA1",
                            Host = "10.81.169.53",
                            Username = "administrator",
                            Password = "P@ssw0rd",
                            Path = "/var/log/CK-111"
                        },
                        new SshConnectionConfig
                        {
                            Name = "UAT.ZES.SCADA2",
                            Host = "10.81.169.54",
                            Username = "administrator",
                            Password = "P@ssw0rd",
                            Path = "/var/log/CK-11"
                        }
                    }
                };

                SaveConfig(defaultConfig);
                return defaultConfig;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки конфига: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return new AppConfig();
        }
    }

    public void SaveConfig(AppConfig config)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            var json = JsonConvert.SerializeObject(config, settings);
            File.WriteAllText(_configFilePath, json);
            Console.WriteLine($"Config saved to: {_configFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения конфига: {ex.Message}");
            throw;
        }
    }

    public void AddOrUpdateConnection(SshConnectionConfig connection)
    {
        var config = LoadConfig();

        // Удаляем существующее подключение с таким же именем
        var existing = config.SshConnections.FirstOrDefault(c =>
            c.Name.Equals(connection.Name, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            config.SshConnections.Remove(existing);
        }

        // Добавляем новое
        config.SshConnections.Add(connection);

        SaveConfig(config);
    }

    public void DeleteConnection(string connectionName)
    {
        var config = LoadConfig();
        var connectionToRemove = config.SshConnections.FirstOrDefault(c =>
            c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

        if (connectionToRemove != null)
        {
            config.SshConnections.Remove(connectionToRemove);
            SaveConfig(config);
        }
    }
}