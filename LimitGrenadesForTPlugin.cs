using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace LimitGrenadesForT;

public class LimitGrenadesForTPlugin : BasePlugin
{
    public override string ModuleName => "LimitGrenadesForT";
    public override string ModuleVersion => "1.1.0";

    // Лимит по каждому типу гранаты отдельно.
    // Поменяйте цифры под себя, 0 = убрать полностью.
    private static readonly Dictionary<string, int> GrenadeLimits = new()
    {
        { "weapon_molotov",      1 },
        { "weapon_incgrenade",   1 },
        { "weapon_flashbang",    1 },
        { "weapon_hegrenade",    1 },
        { "weapon_smokegrenade", 1 },
        { "weapon_decoy",        1 },
    };

    // Как часто проверять инвентарь, пока игрок жив (карта может выдавать
    // гранаты не один раз, а по таймеру/триггерам в течение раунда).
    private const float EnforceIntervalSeconds = 1.0f;

    // Включить подробные логи в консоль сервера для диагностики.
    // После того как всё заработает как надо — поставьте false.
    private const bool DebugLogging = true;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventItemPickup>(OnItemPickup);

        Console.WriteLine("[LimitGrenadesForT] Plugin loaded, version " + ModuleVersion);
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Team != CsTeam.Terrorist)
            return HookResult.Continue;

        var slot = player.Slot;

        if (DebugLogging)
            Console.WriteLine($"[LimitGrenadesForT] Spawn detected for {player.PlayerName} (T), starting enforcement timer");

        // Повторяющийся таймер: раз в секунду срезаем лишнее, пока игрок жив.
        // Так ловим и разовую выдачу карты при спавне, и повторные
        // выдачи/подборы в течение раунда.
        var timer = AddTimer(EnforceIntervalSeconds, () => { }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        timer.Callback = () =>
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !p.IsValid || !p.PawnIsAlive || p.Team != CsTeam.Terrorist)
            {
                timer.Kill();
                return;
            }

            LimitGrenades(p);
        };

        return HookResult.Continue;
    }

    private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Team != CsTeam.Terrorist)
            return HookResult.Continue;

        // Небольшая задержка, чтобы оружие успело появиться в MyWeapons
        AddTimer(0.05f, () => LimitGrenades(player), TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }

    private void LimitGrenades(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive)
            return;

        if (player.Team != CsTeam.Terrorist)
            return;

        var weaponServices = player.PlayerPawn.Value?.WeaponServices;
        if (weaponServices == null) return;

        var allWeapons = weaponServices.MyWeapons
            .Where(w => w.Value != null)
            .ToList();

        if (DebugLogging)
        {
            var names = string.Join(", ", allWeapons.Select(w => w.Value!.DesignerName));
            Console.WriteLine($"[LimitGrenadesForT] {player.PlayerName} inventory: {names}");
        }

        var grenadeGroups = allWeapons
            .Where(w => GrenadeLimits.ContainsKey(w.Value!.DesignerName))
            .GroupBy(w => w.Value!.DesignerName);

        foreach (var group in grenadeGroups)
        {
            var limit = GrenadeLimits[group.Key];
            var toRemove = group.Skip(limit).ToList();

            if (toRemove.Count == 0)
                continue;

            if (DebugLogging)
                Console.WriteLine($"[LimitGrenadesForT] Removing {toRemove.Count}x {group.Key} from {player.PlayerName}");

            foreach (var extra in toRemove)
            {
                extra.Value?.Remove();
            }
        }
    }
}
