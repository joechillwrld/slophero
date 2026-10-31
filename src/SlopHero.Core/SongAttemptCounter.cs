using System.Text.Json;

namespace SlopHero.Core;

/// <summary>Session-only attempt counts, scoped by chart and practice section.</summary>
public sealed class SongAttemptCounter
{
    private sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, int> Songs { get; set; } = new(StringComparer.Ordinal);
        public long GlobalTotal { get; set; }
    }

    private readonly Dictionary<string, int> songs = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Song, int Section), int> sections = new();
    private Dictionary<string, int> savedSongs = new(StringComparer.Ordinal);
    private long savedTotal;
    private string? savePath;
    public string? PersistenceError { get; private set; }
    private nint lastScene;
    private bool wasPlaying;
    private string? lastSongKey;
    private bool lastPractice;
    private double lastSongTime = double.NaN, maxSongTime = double.NaN;
    public long AttemptSequence { get; private set; }
    public int ActiveSection { get; private set; } = -1;

    public void ConfigurePersistence(string path)
    {
        try
        {
            Dictionary<string, int> loaded;
            long total;
            if (!File.Exists(path))
            {
                loaded = new(StringComparer.Ordinal);
                total = 0;
            }
            else
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("Version", out var version))
                {
                    if (version.GetInt32() != 1) throw new InvalidDataException("Unknown attempt-count format.");
                    var data = JsonSerializer.Deserialize<SaveData>(document.RootElement.GetRawText())
                        ?? throw new InvalidDataException("Attempt-count file is empty.");
                    loaded = data.Songs ?? throw new InvalidDataException("Per-song counts are missing.");
                    total = data.GlobalTotal;
                }
                else
                {
                    // 0.4.141 development format: one dictionary with a total
                    // implicitly equal to the sum of its per-song counts.
                    loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(
                        document.RootElement.GetRawText())
                        ?? throw new InvalidDataException("Attempt-count file is empty.");
                    total = loaded.Values.Sum(count => (long)count);
                }
            }
            if (total < 0 || loaded.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0))
                throw new InvalidDataException("Attempt counts contain invalid entries.");
            savedSongs = new(loaded, StringComparer.Ordinal);
            savedTotal = total;
            savePath = path;
            PersistenceError = null;
        }
        catch (Exception ex)
        {
            savePath = null; // Never overwrite a file that could not be read.
            PersistenceError = ex.Message;
        }
    }

    public void Observe(string? songKey, nint scene, bool playing, bool practice, int section,
        bool saveFullSong = false, double songTime = double.NaN, bool? saveGlobalTotal = null)
    {
        if (scene == 0 || string.IsNullOrWhiteSpace(songKey))
            return;
        // The game's isSongPlaying flag goes false while paused and during
        // transitions. Neither state starts a new attempt on resume.
        if (!playing) return;
        bool newContext = !wasPlaying || scene != lastScene || songKey != lastSongKey
            || practice != lastPractice;
        // Restarts near the start of a song can rewind less than a second.
        // Any meaningful backward jump in the monotonic song clock is a new
        // attempt; small clock noise remains below this threshold.
        bool rewound = !newContext && double.IsFinite(songTime) && double.IsFinite(lastSongTime)
            && songTime < lastSongTime - .075;
        if (!newContext && !rewound)
        {
            if (double.IsFinite(songTime))
            {
                lastSongTime = songTime;
                maxSongTime = double.IsFinite(maxSongTime) ? Math.Max(maxSongTime, songTime) : songTime;
            }
            return;
        }
        wasPlaying = true;
        lastScene = scene;
        lastSongKey = songKey;
        lastPractice = practice;
        lastSongTime = maxSongTime = songTime;
        AttemptSequence++;
        ActiveSection = practice ? section : -1;
        if (practice)
        {
            if (section >= 0)
            {
                var key = (songKey, section);
                sections[key] = sections.GetValueOrDefault(key) + 1;
            }
        }
        else
        {
            songs[songKey] = songs.GetValueOrDefault(songKey) + 1;
            bool saveGlobal = saveGlobalTotal ?? saveFullSong;
            if ((saveFullSong || saveGlobal) && savePath != null)
            {
                if (saveFullSong) savedSongs[songKey] = savedSongs.GetValueOrDefault(songKey) + 1;
                if (saveGlobal) savedTotal++;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                    string temp = savePath + ".tmp";
                    File.WriteAllText(temp, JsonSerializer.Serialize(new SaveData
                    { Songs = savedSongs, GlobalTotal = savedTotal }));
                    File.Move(temp, savePath, true);
                    PersistenceError = null;
                }
                catch (Exception ex) { PersistenceError = ex.Message; }
            }
        }
    }

    public void ResetContext()
    {
        wasPlaying = false;
        lastScene = 0;
        lastSongKey = null;
        lastSongTime = maxSongTime = double.NaN;
    }

    public int SongCount(string songKey) => songs.GetValueOrDefault(songKey);
    public int SectionCount(string songKey, int section) => sections.GetValueOrDefault((songKey, section));
    public int SavedSongCount(string songKey) => savedSongs.GetValueOrDefault(songKey);
    public long SavedTotalCount => savedTotal;
}
