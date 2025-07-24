using System.Collections.Generic;
using Newtonsoft.Json;

public class SettingsConfig
{
    [JsonProperty("Servers")]
    public List<Server> Servers { get; set; } = new List<Server>();
}

public class Server
{
    [JsonProperty("Name")]
    public string Name { get; set; }

    [JsonProperty("Host")]
    public string Host { get; set; }

    [JsonProperty("Username")]
    public string Username { get; set; }

    [JsonProperty("Password")]
    public string Password { get; set; }

    [JsonProperty("Path")]
    public string Path { get; set; }

    [JsonProperty("Protocol")]
    public string Protocol { get; set; } = "SSH";

    [JsonProperty("Port")]
    public int Port { get; set; } = 22;

    [JsonProperty("Folder")]
    public string Folder { get; set; } = "Default";
}