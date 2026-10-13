using System.Text.Json;

namespace SlopHero.Mod;

internal sealed class SettingsProfiles
{
    private sealed class Document
    {
        public string Name { get; set; } = "";
        public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string directory;

    public SettingsProfiles(string userDataDirectory)
    {
        directory = Path.Combine(userDataDirectory, "SlopHero", "Profiles");
        Directory.CreateDirectory(directory);
    }

    public string DirectoryPath => directory;

    public string[] List()
    {
        return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    public void Save(string name, Dictionary<string, string> settings)
    {
        string validName = ValidateName(name);
        var document = new Document { Name = validName, Settings = settings };
        string path = GetPath(validName);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temp, path, true);
    }

    public string Rename(string oldName, string newName)
    {
        string oldPath = GetPath(oldName);
        string validNewName = ValidateName(newName);
        string newPath = GetPath(validNewName);
        if (!File.Exists(oldPath)) throw new FileNotFoundException("The selected profile file no longer exists.", oldPath);
        if (string.Equals(oldName, validNewName, StringComparison.Ordinal)) return validNewName;
        if (!string.Equals(oldName, validNewName, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
            throw new IOException($"A profile named '{validNewName}' already exists.");

        // Move through a unique temporary name so case-only renames work on
        // Windows, where the source and destination otherwise alias.
        string temporaryPath = Path.Combine(directory, ".rename-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.Move(oldPath, temporaryPath);
        try { File.Move(temporaryPath, newPath); }
        catch
        {
            if (File.Exists(temporaryPath)) File.Move(temporaryPath, oldPath, true);
            throw;
        }
        return validNewName;
    }

    public bool TryLoad(string name, out Dictionary<string, string> settings)
    {
        settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string path = GetPath(name);
        if (!File.Exists(path)) return false;
        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), JsonOptions);
        if (document?.Settings == null || document.Settings.Count > 128) return false;
        foreach (var pair in document.Settings)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 || pair.Value == null || pair.Value.Length > 512)
                return false;
        }
        settings = new Dictionary<string, string>(document.Settings, StringComparer.OrdinalIgnoreCase);
        return true;
    }

    private string GetPath(string name) => Path.Combine(directory, ValidateName(name) + ".json");

    private static string ValidateName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || !name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_'))
            throw new FormatException("Use 1–40 letters, numbers, spaces, dashes, or underscores for a profile name.");
        return name;
    }
}
