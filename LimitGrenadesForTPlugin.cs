using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace LimitGrenadesForT;

public class LimitGrenadesForTPlugin : BasePlugin
{
    public override string ModuleName => "LimitGrenadesForT";
    public override string ModuleVersion => "1.0.0";

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

    // Задержка после спавна, чтобы карта успела выполнить свою логику выдачи оружия
    private const float StripDelaySeconds = 0.3f;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Team != CsTeam.Terrorist)
            return HookResult.Continue;

        AddTimer(StripDelaySeconds, () => LimitGrenades(player), TimerFlags.STOP_ON_MAPCHANGE);

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

        var grenadeGroups = weaponServices.MyWeapons
            .Where(w => w.Value != null && GrenadeLimits.ContainsKey(w.Value.DesignerName))
            .GroupBy(w => w.Value!.DesignerName);

        foreach (var group in grenadeGroups)
        {
            var limit = GrenadeLimits[group.Key];
            var toRemove = group.Skip(limit);

            foreach (var extra in toRemove)
            {
                extra.Value?.Remove();
            }
        }
    }
}
