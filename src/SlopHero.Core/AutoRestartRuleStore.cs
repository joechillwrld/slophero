using System.Text.Json;

namespace SlopHero.Core;

/// <summary>Per-song restart rules stored outside the game directory.</summary>
public sealed class AutoRestartRuleStore
{
    private readonly string path;
    private readonly Dictionary<string, AutoRestartRule> rules;

    public AutoRestartRuleStore(string path)
    {
        this.path = path;
        rules = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, AutoRestartRule>>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Restart rules file is empty.")
            : new Dictionary<string, AutoRestartRule>(StringComparer.Ordinal);
        if (rules.Any(entry => string.IsNullOrWhiteSpace(entry.Key)
            || entry.Value.MissLimit < 0 || entry.Value.GhostLimit < 0
            || entry.Value.NoMissBeforeSection < 0))
            throw new InvalidDataException("Restart rules contain invalid values.");
    }

    public AutoRestartRule Get(string? songKey) => songKey != null && rules.TryGetValue(songKey, out var rule)
        ? rule : new AutoRestartRule(0, 0, null);

    public void Set(string songKey, AutoRestartRule rule)
    {
        if (string.IsNullOrWhiteSpace(songKey) || rule.MissLimit < 0 || rule.GhostLimit < 0
            || rule.NoMissBeforeSection < 0) throw new ArgumentOutOfRangeException(nameof(rule));
        bool existed = rules.TryGetValue(songKey, out var previous);
        rules[songKey] = rule;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(rules));
            File.Move(temp, path, true);
        }
        catch
        {
            if (existed) rules[songKey] = previous;
            else rules.Remove(songKey);
            throw;
        }
    }
}
