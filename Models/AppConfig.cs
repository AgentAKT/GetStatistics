// Models/AppConfig.cs
using System.Collections.Generic;

public class AppConfig
{
    public List<SshConnectionConfig> SshConnections { get; set; } = new List<SshConnectionConfig>();
}

public class SshConnectionConfig
{
    public string Name { get; set; }
    public string Host { get; set; }
    public int Port { get; set; } = 22;
    public string Username { get; set; }
    public string Password { get; set; }
    public string Path { get; set; }
}