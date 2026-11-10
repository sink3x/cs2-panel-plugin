using System.Text.Json;

namespace CS2Panel;

/// <summary>Reads the JSON files the admin panel writes. A broken file never throws: the plugin falls back to an empty config.</summary>
public static class ConfigReader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static Identity ParseIdentity(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Identity>(json, Options) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public static Dictionary<string, SoundEntry> ParseLibrary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, SoundEntry>>(json, Options) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
