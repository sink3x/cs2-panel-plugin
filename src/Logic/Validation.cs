using System.Text.RegularExpressions;

namespace CS2Panel;

/// <summary>
/// Whitelists for everything that ends up inside a console command. The panel writes the config files, but the plugin
/// still never trusts them: a value that does not match is skipped instead of being sent to the game.
/// </summary>
public static class Validation
{
    private static readonly Regex SoundRx = new(@"^[A-Za-z0-9_./\-]{1,160}$", RegexOptions.Compiled);
    private static readonly Regex TextRx = new(@"^[^""\\;\r\n\x00-\x1f]{0,32}$", RegexOptions.Compiled);
    private static readonly Regex LogoRx = new(@"^[A-Za-z0-9_]{0,24}$", RegexOptions.Compiled);
    private static readonly Regex FlagRx = new(@"^[A-Za-z]{0,2}$", RegexOptions.Compiled);

    /// <summary>A sound path or name that is safe to put into the client command <c>play ...</c>: no spaces, quotes or ';'.</summary>
    public static bool IsSafeSound(string? s) => s != null && SoundRx.IsMatch(s);

    /// <summary>Team name, clan tag or nickname: up to 32 characters, no quotes, backslashes, ';' or control characters.</summary>
    public static bool IsSafeText(string? s) => TextRx.IsMatch(s ?? "");

    /// <summary>Team logo code (navi, faze, ...): up to 24 letters, digits or '_'.</summary>
    public static bool IsSafeLogo(string? s) => LogoRx.IsMatch(s ?? "");

    /// <summary>Country flag: up to two letters.</summary>
    public static bool IsSafeFlag(string? s) => FlagRx.IsMatch(s ?? "");
}
