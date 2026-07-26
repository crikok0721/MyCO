namespace MyCodex.Configuration;

public sealed class ConfigPaths
{
    public ConfigPaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory ??
                        Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "MyCodex");
    }

    public string BaseDirectory { get; }
    public string ConfigFile => Path.Combine(BaseDirectory, "config.json");
    public string CalibrationFile => Path.Combine(BaseDirectory, "calibration.json");
    public string AvatarsDirectory => Path.Combine(BaseDirectory, "avatars");
    public string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    public string BackupsDirectory => Path.Combine(BaseDirectory, "backups");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(AvatarsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
