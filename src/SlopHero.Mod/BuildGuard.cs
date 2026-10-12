using System.Security.Cryptography;

namespace SlopHero.Mod;

internal static class BuildGuard
{
    // These are compatibility fingerprints of the supplied game files, not
    // artifact downloads. A different build requires remapping its game APIs.
    private const string NativeHash = "12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197";
    private const string MetadataHash = "5f14231810f2641639167eb72956e85cced2f36cb4a84682bdefb1e9da3e3d42";

    public static bool Matches(out string reason)
    {
        try
        {
            string root = Path.GetDirectoryName(Environment.ProcessPath)!;
            string native = Path.Combine(root, "GameAssembly.dll");
            string? metadata = Directory.EnumerateDirectories(root, "*_Data")
                .Select(d => Path.Combine(d, "il2cpp_data", "Metadata", "global-metadata.dat"))
                .FirstOrDefault(File.Exists);
            if (!File.Exists(native) || metadata == null)
            {
                reason = "Cannot locate the game binary or metadata beside the running game.";
                return false;
            }
            if (Hash(native) != NativeHash || Hash(metadata) != MetadataHash)
            {
                reason = "Game files differ from the supplied v1.1.0.6142 build; SlopHero is disabled.";
                return false;
            }
            reason = "Matched supplied v1.1.0.6142 game files.";
            return true;
        }
        catch (Exception ex)
        {
            reason = $"Cannot verify the game build: {ex.Message}";
            return false;
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}
