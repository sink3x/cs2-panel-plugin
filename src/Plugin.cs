using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace CS2Panel;

/// <summary>Записи библиотеки звуков: имя → что проиграть клиенту командой play.</summary>
public sealed class SoundEntry
{
    public string Play { get; set; } = "";
    public string Title { get; set; } = "";
}

[MinimumApiVersion(200)]
public class CS2PanelPlugin : BasePlugin
{
    public override string ModuleName => "CS2Panel";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "sink3x";
    public override string ModuleDescription => "Звуки по команде и из админ-панели (cs2-yc-sandbox)";

    // Только то, что безопасно подставить в клиентскую команду «play …»: путь/имя звука без пробелов, кавычек и «;».
    private static readonly Regex SafeSound = new(@"^[A-Za-z0-9_./\-]{1,160}$", RegexOptions.Compiled);
    private const double CooldownSeconds = 2.0;
    private DateTime _lastPlay = DateTime.MinValue;

    private string LibraryFile => Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "plugins", "CS2Panel", "sounds.json");

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[CS2Panel] loaded v{ModuleVersion}");
    }

    private Dictionary<string, SoundEntry> ReadLibrary()
    {
        try
        {
            if (!File.Exists(LibraryFile)) return new();
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Dictionary<string, SoundEntry>>(File.ReadAllText(LibraryFile), opt) ?? new();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[CS2Panel] cannot read sounds.json: {e.Message}");
            return new();
        }
    }

    private static List<CCSPlayerController> RealPlayers() =>
        Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false, Connected: PlayerConnectedState.PlayerConnected }).ToList();

    /// <summary>Кому играть: all | me | #userid | часть ника.</summary>
    private static List<CCSPlayerController> Targets(string spec, CCSPlayerController? caller)
    {
        var all = RealPlayers();
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("all", StringComparison.OrdinalIgnoreCase)) return all;
        if (spec.Equals("me", StringComparison.OrdinalIgnoreCase)) return caller is { IsValid: true } ? new() { caller } : new();
        if (spec.StartsWith('#') && int.TryParse(spec.AsSpan(1), out var uid)) return all.Where(p => p.UserId == uid).ToList();
        return all.Where(p => p.PlayerName.Contains(spec, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static void Reply(CCSPlayerController? caller, string text)
    {
        if (caller is { IsValid: true }) caller.PrintToChat($" \x04[CS2Panel]\x01 {text}");
        else Console.WriteLine($"[CS2Panel] {text}");
    }

    [ConsoleCommand("css_playsound", "Проиграть звук: css_playsound <имя из библиотеки | путь/событие> [all|me|#userid|ник]")]
    [CommandHelper(minArgs: 1, usage: "<sound> [all|me|#userid|name]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/generic")]
    public void OnPlaySound(CCSPlayerController? caller, CommandInfo command)
    {
        var what = command.GetArg(1);
        var target = command.ArgCount > 2 ? command.GetArg(2) : "all";
        var lib = ReadLibrary();
        var play = lib.TryGetValue(what, out var entry) ? entry.Play : what;
        if (!SafeSound.IsMatch(play)) { Reply(caller, "Недопустимое имя звука (только буквы, цифры, _ . / -)"); return; }
        if ((DateTime.UtcNow - _lastPlay).TotalSeconds < CooldownSeconds) { Reply(caller, "Слишком часто, подождите пару секунд"); return; }
        var list = Targets(target, caller);
        if (list.Count == 0) { Reply(caller, "Нет подходящих игроков"); return; }
        _lastPlay = DateTime.UtcNow;
        foreach (var p in list) p.ExecuteClientCommand($"play {play}");
        Reply(caller, $"Звук «{what}» → {list.Count} игр.");
    }

    // Короткая команда для чата: !snd <имя> [кому]; без аргументов — список библиотеки.
    [ConsoleCommand("css_snd", "Звук из библиотеки: !snd <имя> [all|me|#userid|ник]")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/generic")]
    public void OnSnd(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2) { OnSounds(caller, command); return; }
        OnPlaySound(caller, command);
    }

    [ConsoleCommand("css_sounds", "Список звуков из библиотеки")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/generic")]
    public void OnSounds(CCSPlayerController? caller, CommandInfo command)
    {
        var lib = ReadLibrary();
        Reply(caller, lib.Count == 0 ? "Библиотека пуста: добавьте звуки в админ-панели (страница «Звуки»)" : "Звуки: " + string.Join(", ", lib.Keys.Take(40)));
    }
}
