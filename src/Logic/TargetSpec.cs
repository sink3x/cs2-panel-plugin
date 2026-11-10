namespace CS2Panel;

public enum TargetKind { All, Me, UserId, SteamId, Name }

/// <summary>Who a command is aimed at: <c>all</c> | <c>me</c> | <c>#userid</c> | SteamID64 | part of a nickname.</summary>
public readonly record struct TargetSpec(TargetKind Kind, int UserId = 0, ulong SteamId = 0, string Name = "")
{
    public static TargetSpec Parse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("all", StringComparison.OrdinalIgnoreCase)) return new(TargetKind.All);
        if (spec.Equals("me", StringComparison.OrdinalIgnoreCase)) return new(TargetKind.Me);
        if (spec.StartsWith('#') && int.TryParse(spec.AsSpan(1), out var uid)) return new(TargetKind.UserId, UserId: uid);
        if (spec.Length == 17 && ulong.TryParse(spec, out var sid)) return new(TargetKind.SteamId, SteamId: sid);
        return new(TargetKind.Name, Name: spec);
    }
}
