using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

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
    public override string ModuleVersion => "0.3.0";
    public override string ModuleAuthor => "sink3x";
    public override string ModuleDescription => "Звуки, животные и обновление скинов по команде и из админ-панели (cs2-yc-sandbox)";

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
        Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList();

    /// <summary>Кому играть: all | me | #userid | часть ника.</summary>
    private static List<CCSPlayerController> Targets(string spec, CCSPlayerController? caller)
    {
        var all = RealPlayers();
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("all", StringComparison.OrdinalIgnoreCase)) return all;
        if (spec.Equals("me", StringComparison.OrdinalIgnoreCase)) return caller is { IsValid: true } ? new() { caller } : new();
        if (spec.StartsWith('#') && int.TryParse(spec.AsSpan(1), out var uid)) return all.Where(p => p.UserId == uid).ToList();
        if (spec.Length == 17 && ulong.TryParse(spec, out var sid)) return all.Where(p => p.SteamID == sid).ToList();
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

    // ---- скины ----------------------------------------------------------------------------------------------------
    // WeaponPaints читает базу при заходе игрока и по его команде !wp. Панель пишет скины в базу и просит плагин нажать !wp за игрока,
    // чтобы изменения применились сразу, без перезахода. (Если в WeaponPaints.json большая CmdRefreshCooldownSeconds, повтор раньше срока он отклонит.)
    [ConsoleCommand("css_refreshskins", "Применить скины из базы сразу (!wp за игрока): css_refreshskins [all|me|#userid|steamid64|ник]")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/generic")]
    public void OnRefreshSkins(CCSPlayerController? caller, CommandInfo command)
    {
        var list = Targets(command.ArgCount > 1 ? command.GetArg(1) : "all", caller);
        if (list.Count == 0) { Reply(caller, "Нет подходящих игроков"); return; }
        foreach (var p in list) p.ExecuteClientCommandFromServer("css_wp");
        Reply(caller, $"Скины обновлены: {list.Count} игр.");
    }

    // ---- животные -------------------------------------------------------------------------------------------------
    // ent_create из RCON не работает: у консоли сервера нет игрока, а животное создаётся «у прицела» вызвавшего. Поэтому создаём сами,
    // рядом с живым игроком. В CS2 есть только курица; «свинья» и «корова» — та же курица, перекрашенная и увеличенная.
    private const int MaxAnimals = 60;
    private static readonly Dictionary<string, (Color? Color, float Scale)> Animals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chicken"] = (null, 1f),
        ["pig"] = (Color.FromArgb(255, 255, 150, 175), 2.2f),
        ["cow"] = (Color.FromArgb(255, 245, 245, 245), 3.2f),
    };

    /// <summary>Игрок, рядом с которым создаём: me/auto — вызвавший (иначе первый живой), #userid или часть ника.</summary>
    private static CCSPlayerController? PickPlayer(string spec, CCSPlayerController? caller)
    {
        var alive = RealPlayers().Where(p => p.PawnIsAlive && p.PlayerPawn.Value is { IsValid: true }).ToList();
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("auto", StringComparison.OrdinalIgnoreCase) || spec.Equals("me", StringComparison.OrdinalIgnoreCase))
            return caller is { IsValid: true } && alive.Any(p => p.Slot == caller.Slot) ? caller : alive.FirstOrDefault();
        if (spec.StartsWith('#') && int.TryParse(spec.AsSpan(1), out var uid)) return alive.FirstOrDefault(p => p.UserId == uid);
        return alive.FirstOrDefault(p => p.PlayerName.Contains(spec, StringComparison.OrdinalIgnoreCase));
    }

    [ConsoleCommand("css_spawnanimal", "Животные рядом с игроком: css_spawnanimal [chicken|pig|cow] [1-30] [auto|me|#userid|ник]")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/cheats")]
    public void OnSpawnAnimal(CCSPlayerController? caller, CommandInfo command)
    {
        var kind = command.ArgCount > 1 ? command.GetArg(1).ToLowerInvariant() : "chicken";
        if (!Animals.TryGetValue(kind, out var an)) { Reply(caller, "Доступны: chicken, pig, cow"); return; }
        var count = 1;
        if (command.ArgCount > 2 && int.TryParse(command.GetArg(2), out var c)) count = Math.Clamp(c, 1, 30);
        var spec = command.ArgCount > 3 ? command.GetArg(3) : "auto";
        var pawn = PickPlayer(spec, caller)?.PlayerPawn.Value;
        var origin = pawn?.AbsOrigin;
        if (pawn == null || origin == null) { Reply(caller, "Нет живого игрока: животных создают рядом с игроком (зайдите на сервер и появитесь в игре)"); return; }
        var existing = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("chicken").Count();
        if (existing + count > MaxAnimals) { Reply(caller, $"Слишком много животных (на карте {existing}, максимум {MaxAnimals}): уберите лишних командой css_killanimals"); return; }

        var yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
        var made = 0;
        for (var i = 0; i < count; i++)
        {
            var ang = yaw + (Random.Shared.NextSingle() - 0.5f) * 1.6f; // впереди игрока, веером
            var dist = 90f + Random.Shared.NextSingle() * 90f;
            var pos = new Vector(origin.X + MathF.Cos(ang) * dist, origin.Y + MathF.Sin(ang) * dist, origin.Z + 20f);
            var e = Utilities.CreateEntityByName<CBaseModelEntity>("chicken");
            if (e == null || !e.IsValid) continue;
            e.Teleport(pos, new QAngle(0, Random.Shared.NextSingle() * 360f, 0), new Vector(0, 0, 0));
            e.DispatchSpawn();
            if (an.Color is { } col) { e.Render = col; Utilities.SetStateChanged(e, "CBaseModelEntity", "m_clrRender"); }
            if (an.Scale > 1f) e.AcceptInput("AddOutput", value: $"modelscale {an.Scale.ToString(CultureInfo.InvariantCulture)}");
            made++;
        }
        Reply(caller, made == 0 ? "Не удалось создать животных" : $"Создано: {made} ({kind}) рядом с {PickPlayer(spec, caller)?.PlayerName}");
    }

    [ConsoleCommand("css_killanimals", "Убрать всех куриц (и свиней/коров) с карты")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/cheats")]
    public void OnKillAnimals(CCSPlayerController? caller, CommandInfo command)
    {
        var n = 0;
        foreach (var e in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("chicken").ToList())
        {
            if (!e.IsValid) continue;
            e.Remove(); n++;
        }
        Reply(caller, $"Убрано животных: {n}");
    }
}
