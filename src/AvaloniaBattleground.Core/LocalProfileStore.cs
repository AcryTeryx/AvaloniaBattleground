namespace AvaloniaBattleground.Core;

using System.Text.Json;

public sealed class LocalProfileStore
{
    public const string DefaultDisplayName = "Player";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _profilePath;

    public LocalProfileStore(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("Profile path is required.", nameof(profilePath));
        }

        _profilePath = profilePath;
    }

    public static string GetDefaultProfilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AvaloniaBattleground",
            "profile.json");
    }

    public LocalProfile Load()
    {
        if (!File.Exists(_profilePath))
        {
            return new LocalProfile(DefaultDisplayName);
        }

        using var profileStream = File.OpenRead(_profilePath);
        var profile = JsonSerializer.Deserialize<LocalProfile>(profileStream, SerializerOptions);

        return Normalize(profile);
    }

    public void Save(LocalProfile profile)
    {
        var profileDirectory = Path.GetDirectoryName(_profilePath);
        if (!string.IsNullOrWhiteSpace(profileDirectory))
        {
            Directory.CreateDirectory(profileDirectory);
        }

        using var profileStream = File.Create(_profilePath);
        JsonSerializer.Serialize(profileStream, Normalize(profile), SerializerOptions);
    }

    public static string NormalizeDisplayName(string? displayName)
    {
        var normalizedDisplayName = displayName?.Trim();
        return string.IsNullOrWhiteSpace(normalizedDisplayName)
            ? DefaultDisplayName
            : normalizedDisplayName;
    }

    private static LocalProfile Normalize(LocalProfile? profile)
    {
        return new LocalProfile(NormalizeDisplayName(profile?.DisplayName));
    }
}
