using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class ServerConfig
{
    public string Name { get; set; }
    public string Host { get; set; }
    public string Protocol { get; set; }
    public string Path { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public int? Port { get; set; } // Делаем nullable, так как не у всех серверов есть порт
    public string IconPath => Protocol == "Local" ? "local_icon.png" : "ssh_icon.png";
}

public class ServerFolder
{
    public string Name { get; set; }
    public List<ServerConfig> Servers { get; set; }
    public string IconPath => "folder_icon.png";
}

public class Config
{
    public List<ServerFolder> Folders { get; set; }
    public List<string> String1 { get; set; }
    public List<string> String2 { get; set; }

    public static Config Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<Config>(json);
    }

    public void Save(string path)
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}