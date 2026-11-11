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
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2Panel;

[MinimumApiVersion(200)]
public class CS2PanelPlugin : BasePlugin
{
    public override string ModuleName => "CS2Panel";
    public override string ModuleVersion => "0.6.0";
    public override string ModuleAuthor => "sink3x";
    public override string ModuleDescription => "Panel companion: in-game !menu, sounds, team names/logos, clan tags, animals, map cleanup (cs2-server-admin-panel)";

    private const double CooldownSeconds = 2.0;
    private DateTime _lastPlay = DateTime.MinValue;

    private string LibraryFile => Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "plugins", "CS2Panel", "sounds.json");

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[CS2Panel] loaded v{ModuleVersion}");
        _identity = ReadIdentity();
        RegisterListener<Listeners.OnMapStart>(_ => AddTimer(2.0f, () => { _identity = ReadIdentity(); ApplyTeams(); }));
        // клиент время от времени возвращает свой клан-тег из Steam: периодически ставим наш заново
        AddTimer(10.0f, ApplyPlayers, TimerFlags.REPEAT);
        if (hotReload) { ApplyTeams(); ApplyPlayers(); }
    }

    // ---- оформление: названия и логотипы команд, клан-теги и ники ------------------------------------------------------
    // Панель пишет identity.json и вызывает css_identity. Логотип — код команды из файлов игры (navi, faze, vita…):
    // свою картинку сервер игрокам показать не может, клиент рисует только то, что у него уже есть.
    private Identity _identity = new();
    private string IdentityFile => Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "plugins", "CS2Panel", "identity.json");

    private Identity ReadIdentity()
    {
        try { return File.Exists(IdentityFile) ? ConfigReader.ParseIdentity(File.ReadAllText(IdentityFile)) : new(); }
        catch (IOException e) { Console.WriteLine($"[CS2Panel] cannot read identity.json: {e.Message}"); return new(); }
    }

    private void ApplyTeams()
    {
        // mp_teamname_1 — CT, mp_teamname_2 — T (так устроена игра)
        foreach (var (key, n) in new[] { ("ct", 1), ("t", 2) })
        {
            var t = _identity.Teams.TryGetValue(key, out var look) ? look : new TeamLook();
            if (!Validation.IsSafeText(t.Name) || !Validation.IsSafeLogo(t.Logo) || !Validation.IsSafeFlag(t.Flag)) continue;
            Server.ExecuteCommand($"mp_teamname_{n} \"{t.Name}\"");
            Server.ExecuteCommand($"mp_teamlogo_{n} \"{t.Logo}\"");
            Server.ExecuteCommand($"mp_teamflag_{n} \"{(t.Flag ?? "").ToUpperInvariant()}\"");
        }
    }

    private bool ApplyPlayer(CCSPlayerController p)
    {
        if (p is not { IsValid: true, IsBot: false, IsHLTV: false }) return false;
        if (!_identity.Players.TryGetValue(p.SteamID.ToString(), out var look)) return false;
        var changed = false;
        if (Validation.IsSafeText(look.Tag) && !string.IsNullOrEmpty(look.Tag) && p.Clan != look.Tag)
        {
            p.Clan = look.Tag; Utilities.SetStateChanged(p, "CCSPlayerController", "m_szClan"); changed = true;
        }
        if (Validation.IsSafeText(look.Name) && !string.IsNullOrWhiteSpace(look.Name) && p.PlayerName != look.Name)
        {
            p.PlayerName = look.Name; Utilities.SetStateChanged(p, "CBasePlayerController", "m_iszPlayerName"); changed = true;
        }
        return changed;
    }

    private void ApplyPlayers()
    {
        var changed = false;
        foreach (var p in RealPlayers()) changed |= ApplyPlayer(p);
        // таблица счёта перечитывает теги и ники по этому событию
        if (changed) new EventNextlevelChanged(false).FireEvent(false);
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        AddTimer(1.5f, ApplyPlayers);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn ev, GameEventInfo info)
    {
        AddTimer(0.5f, ApplyPlayers);
        return HookResult.Continue;
    }

    [ConsoleCommand("css_identity", "Перечитать identity.json и применить названия команд, клан-теги и ники")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnIdentity(CCSPlayerController? caller, CommandInfo command)
    {
        _identity = ReadIdentity();
        ApplyTeams(); ApplyPlayers();
        Reply(caller, $"Оформление применено: игроков с тегом или ником {_identity.Players.Count}");
    }

    private Dictionary<string, SoundEntry> ReadLibrary()
    {
        try { return File.Exists(LibraryFile) ? ConfigReader.ParseLibrary(File.ReadAllText(LibraryFile)) : new(); }
        catch (IOException e) { Console.WriteLine($"[CS2Panel] cannot read sounds.json: {e.Message}"); return new(); }
    }

    private static List<CCSPlayerController> RealPlayers() =>
        Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList();

    /// <summary>Кому играть: all | me | #userid | часть ника.</summary>
    private static List<CCSPlayerController> Targets(string spec, CCSPlayerController? caller)
    {
        var all = RealPlayers();
        var t = TargetSpec.Parse(spec);
        return t.Kind switch
        {
            TargetKind.All => all,
            TargetKind.Me => caller is { IsValid: true } ? new() { caller } : new(),
            TargetKind.UserId => all.Where(p => p.UserId == t.UserId).ToList(),
            TargetKind.SteamId => all.Where(p => p.SteamID == t.SteamId).ToList(),
            _ => all.Where(p => p.PlayerName.Contains(t.Name, StringComparison.OrdinalIgnoreCase)).ToList(),
        };
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
        if (!Validation.IsSafeSound(play)) { Reply(caller, "Недопустимое имя звука (только буквы, цифры, _ . / -)"); return; }
        if (caller != null && (DateTime.UtcNow - _lastPlay).TotalSeconds < CooldownSeconds) { Reply(caller, "Слишком часто, подождите пару секунд"); return; }
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

    // ---- меню сервера ---------------------------------------------------------------------------------------------
    // !menu → меню по центру экрана (пункты выбираются цифрами 1–9). Разделы зависят от прав игрока.
    // Чужие команды (скины WeaponPaints, тренировка MatchZy) выполняются от имени игрока: права проверяет сам плагин-владелец.
    private static bool Has(CCSPlayerController p, string flag) => AdminManager.PlayerHasPermissions(p, flag);
    private static readonly (string Map, string Title)[] MenuMaps =
    {
        ("de_dust2", "Dust II"), ("de_mirage", "Mirage"), ("de_inferno", "Inferno"), ("de_nuke", "Nuke"), ("de_overpass", "Overpass"),
        ("de_ancient", "Ancient"), ("de_anubis", "Anubis"), ("de_train", "Train"), ("de_vertigo", "Vertigo"), ("cs_office", "Office"), ("cs_italy", "Italy"),
    };

    private CenterHtmlMenu NewMenu(string title, Action<CCSPlayerController>? back)
    {
        var menu = new CenterHtmlMenu(title, this);
        if (back != null) menu.AddMenuOption("← Назад", (p, _) => back(p));
        return menu;
    }
    private void Show(CCSPlayerController p, CenterHtmlMenu menu) { if (p is { IsValid: true }) MenuManager.OpenCenterHtmlMenu(this, p, menu); }
    private static void Run(CCSPlayerController p, string cmd) { if (p is { IsValid: true }) p.ExecuteClientCommandFromServer(cmd); }

    [ConsoleCommand("css_menu", "Меню сервера: !menu")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnMenu(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller is { IsValid: true }) OpenMainMenu(caller);
    }

    private void OpenMainMenu(CCSPlayerController caller)
    {
        var menu = NewMenu("Меню сервера", null);
        menu.AddMenuOption("Скины и внешний вид", (p, _) => OpenSkinsMenu(p));
        menu.AddMenuOption("Команда", (p, _) => OpenTeamMenu(p));
        menu.AddMenuOption("Тренировка", (p, _) => OpenPracticeMenu(p));
        if (Has(caller, "@css/generic")) menu.AddMenuOption("Раунд и боты", (p, _) => OpenRoundMenu(p));
        if (Has(caller, "@css/changemap")) menu.AddMenuOption("Сменить карту", (p, _) => OpenMapsMenu(p));
        if (Has(caller, "@css/generic")) menu.AddMenuOption("Звуки", (p, _) => OpenSoundsMenu(p));
        if (Has(caller, "@css/cheats")) menu.AddMenuOption("Животные и очистка", (p, _) => OpenAnimalsMenu(p));
        Show(caller, menu);
    }

    private void OpenSkinsMenu(CCSPlayerController caller)
    {
        var menu = NewMenu("Скины и внешний вид", OpenMainMenu);
        foreach (var (title, cmd) in new[] { ("Скины оружия", "css_skins"), ("Нож", "css_knife"), ("Перчатки", "css_gloves"), ("Агент", "css_agents"), ("Музыка", "css_music"), ("Значок", "css_pins"), ("Применить скины из панели", "css_wp") })
            menu.AddMenuOption(title, (p, _) => Run(p, cmd));
        Show(caller, menu);
    }

    private void OpenTeamMenu(CCSPlayerController caller)
    {
        var menu = NewMenu("Команда", OpenMainMenu);
        menu.AddMenuOption("За спецназ (CT)", (p, _) => p.ChangeTeam(CsTeam.CounterTerrorist));
        menu.AddMenuOption("За террористов (T)", (p, _) => p.ChangeTeam(CsTeam.Terrorist));
        menu.AddMenuOption("В наблюдатели", (p, _) => p.ChangeTeam(CsTeam.Spectator));
        menu.AddMenuOption("Вид от третьего лица", (p, _) => Run(p, "css_tp"));
        Show(caller, menu);
    }

    private void OpenPracticeMenu(CCSPlayerController caller)
    {
        var menu = NewMenu("Тренировка (MatchZy)", OpenMainMenu);
        foreach (var (title, cmd) in new[] { ("Включить тренировку", "css_prac"), ("Выйти из тренировки", "css_exitprac"), ("Поставить бота здесь", "css_bot"), ("Убрать ботов", "css_nobots"), ("Повторить бросок гранаты", "css_rethrow"), ("Убрать дымы и огонь", "css_clear"), ("Готов к матчу", "css_ready") })
            menu.AddMenuOption(title, (p, _) => Run(p, cmd));
        Show(caller, menu);
    }

    private void OpenRoundMenu(CCSPlayerController caller)
    {
        if (!Has(caller, "@css/generic")) return;
        var menu = NewMenu("Раунд и боты", OpenMainMenu);
        foreach (var (title, cmd) in new[] { ("Рестарт раунда", "mp_restartgame 1"), ("Закончить разминку", "mp_warmup_end"), ("Поменять стороны", "mp_swapteams"), ("Бот за CT", "bot_add_ct"), ("Бот за T", "bot_add_t"), ("Убрать ботов", "bot_kick") })
            menu.AddMenuOption(title, (p, _) => { if (Has(p, "@css/generic")) Server.ExecuteCommand(cmd); });
        Show(caller, menu);
    }

    private void OpenMapsMenu(CCSPlayerController caller)
    {
        if (!Has(caller, "@css/changemap")) return;
        var menu = NewMenu("Сменить карту", OpenMainMenu);
        foreach (var (map, title) in MenuMaps)
            menu.AddMenuOption(title, (p, _) => { if (Has(p, "@css/changemap")) Server.ExecuteCommand($"changelevel {map}"); });
        Show(caller, menu);
    }

    private void OpenSoundsMenu(CCSPlayerController caller)
    {
        if (!Has(caller, "@css/generic")) return;
        var lib = ReadLibrary().Where(kv => Validation.IsSafeSound(kv.Key)).Take(30).ToList();
        if (lib.Count == 0) { Reply(caller, "Библиотека звуков пуста (страница «Звуки» в админ-панели)"); return; }
        var menu = NewMenu("Звук для всех", OpenMainMenu);
        foreach (var kv in lib)
        {
            var name = kv.Key;
            menu.AddMenuOption(string.IsNullOrWhiteSpace(kv.Value.Title) ? name : kv.Value.Title, (p, _) => { if (Has(p, "@css/generic")) Server.ExecuteCommand($"css_playsound {name} all"); });
        }
        Show(caller, menu);
    }

    private void OpenAnimalsMenu(CCSPlayerController caller)
    {
        if (!Has(caller, "@css/cheats")) return;
        var menu = NewMenu("Животные и очистка", OpenMainMenu);
        foreach (var (kind, label) in new[] { ("chicken", "Курицы ×5"), ("pig", "Свиньи ×3"), ("cow", "Корова ×1") })
        {
            var n = kind == "chicken" ? 5 : kind == "pig" ? 3 : 1;
            menu.AddMenuOption(label, (p, _) => { if (Has(p, "@css/cheats")) Server.ExecuteCommand($"css_spawnanimal {kind} {n} #{p.UserId}"); });
        }
        menu.AddMenuOption("Убрать всех животных", (p, _) => { if (Has(p, "@css/cheats")) Server.ExecuteCommand("css_killanimals"); });
        menu.AddMenuOption("Очистить карту от гранат и хлама", (p, _) => { if (Has(p, "@css/cheats")) Server.ExecuteCommand("css_cleanup all"); });
        Show(caller, menu);
    }

    // ---- очистка карты --------------------------------------------------------------------------------------------
    // Гранаты и дымы (летящие и уже сработавшие), огонь молотовов, оружие на полу, следы пуль и спреи.
    // Следы пуль и спреи рисует КЛИЕНТ, сервер их убрать не может — просим у игроков r_cleardecals (если клиент разрешит).
    private static readonly string[] Projectiles = { "smokegrenade_projectile", "hegrenade_projectile", "flashbang_projectile", "molotov_projectile", "incendiarygrenade_projectile", "decoy_projectile" };

    private static int RemoveAll<T>(string designer, Func<T, bool>? filter = null) where T : CBaseEntity
    {
        var n = 0;
        foreach (var e in Utilities.FindAllEntitiesByDesignerName<T>(designer).ToList())
        {
            if (!e.IsValid || (filter != null && !filter(e))) continue;
            e.Remove(); n++;
        }
        return n;
    }

    [ConsoleCommand("css_cleanup", "Очистка карты: css_cleanup [grenades|fire|weapons|decals|all]")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/cheats")]
    public void OnCleanup(CCSPlayerController? caller, CommandInfo command)
    {
        var what = (command.ArgCount > 1 ? command.GetArg(1) : "all").ToLowerInvariant();
        if (what is not ("grenades" or "fire" or "weapons" or "decals" or "all")) { Reply(caller, "Доступно: grenades, fire, weapons, decals, all"); return; }
        var all = what == "all"; var parts = new List<string>();
        if (all || what == "grenades") { var n = 0; foreach (var d in Projectiles) n += RemoveAll<CBaseEntity>(d); parts.Add($"гранат и дымов: {n}"); }
        if (all || what == "fire") parts.Add($"огня: {RemoveAll<CBaseEntity>("inferno")}");
        if (all || what == "weapons")
            parts.Add($"оружия на полу: {RemoveAll<CCSWeaponBase>("weapon_", w => w.OwnerEntity.Value == null && !w.DesignerName.Equals("weapon_c4", StringComparison.OrdinalIgnoreCase))}");
        if (all || what == "decals")
        {
            var pl = RealPlayers();
            foreach (var p in pl) p.ExecuteClientCommandFromServer("r_cleardecals");
            parts.Add($"следы пуль и спреи: запрос {pl.Count} игрокам");
        }
        Reply(caller, "Очистка — " + string.Join(", ", parts));
    }

    // ---- животные -------------------------------------------------------------------------------------------------
    // ent_create из RCON не работает: у консоли сервера нет игрока, а животное создаётся «у прицела» вызвавшего. Поэтому создаём сами,
    // рядом с живым игроком. В CS2 есть только курица; «свинья» и «корова» — та же курица, перекрашенная и увеличенная.
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
        if (!AnimalRules.IsKnown(kind) || !Animals.TryGetValue(kind, out var an)) { Reply(caller, "Доступны: chicken, pig, cow"); return; }
        var count = 1;
        if (command.ArgCount > 2 && int.TryParse(command.GetArg(2), out var c)) count = AnimalRules.ClampCount(c);
        var spec = command.ArgCount > 3 ? command.GetArg(3) : "auto";
        var pawn = PickPlayer(spec, caller)?.PlayerPawn.Value;
        var origin = pawn?.AbsOrigin;
        if (pawn == null || origin == null) { Reply(caller, "Нет живого игрока: животных создают рядом с игроком (зайдите на сервер и появитесь в игре)"); return; }
        var existing = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("chicken").Count();
        if (!AnimalRules.Fits(existing, count)) { Reply(caller, $"Слишком много животных (на карте {existing}, максимум {AnimalRules.MaxAnimals}): уберите лишних командой css_killanimals"); return; }

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
