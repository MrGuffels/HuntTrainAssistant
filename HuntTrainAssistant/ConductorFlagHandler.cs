using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.CSExtensions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using HuntTrainAssistant.Tasks;

namespace HuntTrainAssistant;

/// <summary>
///     Owns everything that happens in reaction to a conductor's flag: opening the map, deciding whether
///     to teleport or walk, and following up with mount + move-to-flag. Kept separate from
///     <see cref="ChatMessageHandler"/> so that S-rank/self chat traffic never touches auto-movement.
/// </summary>
internal unsafe static class ConductorFlagHandler
{
    internal static ArrivalData LastMessageLoc = null;

    private static (MapLinkPayload Link, Aetheryte NearestAetheryte)? _pendingFlag;
    private static bool _wasInCombat;

    internal static void OnConductorFlag(MapLinkPayload m)
    {
        var nearestAetheryte = MapManager.GetNearestAetheryte(m);
        if(nearestAetheryte == null) return;

        if(P.Config.AutoOpenMap)
            OpenMapIfNeeded(m);

        LastMessageLoc = ArrivalData.CreateOrNull(nearestAetheryte, m.TerritoryType.RowId, 0, isConductorTriggered: true);

        if(!P.Config.AutoTeleport) return;

        // Cross-zone and instance-switch teleports fire immediately, same as before this feature existed --
        // the actual teleport cast already waits for combat to end elsewhere (HuntTrainAssistant.Framework_Update).
        if(m.TerritoryType.RowId != Svc.ClientState.TerritoryType)
        {
            TeleportTo(m, nearestAetheryte.Value, P.Config.AutoSwitchInstanceToOne ? 1 : 0);
            return;
        }

        if(Utils.CanAutoInstanceSwitch() && P.Config.AutoSwitchInstanceTwoRanks &&
           S.LifestreamIPC.GetCurrentInstance() < S.LifestreamIPC.GetNumberOfInstances())
        {
            TeleportTo(m, nearestAetheryte.Value, S.LifestreamIPC.GetCurrentInstance() + 1);
            return;
        }

        if(!P.Config.UseMoveToFlag) return;

        // Same zone/instance: only the new walk-vs-teleport decision needs a settled, out-of-combat position.
        if(Svc.Condition[ConditionFlag.InCombat])
        {
            _pendingFlag = (m, nearestAetheryte.Value);
            return;
        }

        DecideWalkOrTeleport(m, nearestAetheryte.Value);
    }

    /// <summary>Called every frame from Framework_Update to catch the moment combat ends.</summary>
    internal static void Update()
    {
        if(_pendingFlag == null) return;

        bool inCombat = Svc.Condition[ConditionFlag.InCombat];
        if(_wasInCombat && !inCombat)
        {
            var (link, aetheryte) = _pendingFlag.Value;
            _pendingFlag = null;
            DecideWalkOrTeleport(link, aetheryte);
        }
        _wasInCombat = inCombat;
    }

    private static void OpenMapIfNeeded(MapLinkPayload m)
    {
        var flag = AgentMap.Instance()->FlagMapMarker;
        if(AgentMap.Instance()->IsFlagMarkerSet != false && flag.TerritoryId == m.TerritoryType.RowId)
        {
            if(Svc.Data.GetExcelSheet<Map>().TryGetFirst(x => x.TerritoryType.RowId == m.TerritoryType.RowId, out var place))
            {
                var pos = new Vector2(m.RawX / 1000, m.RawY / 1000);
                var distance = Vector2.Distance(new(flag.XFloat, flag.YFloat), pos);
                PluginLog.Information($"Distance between map marker and linked position is {distance}");
                if(distance > 10 || !P.Config.NoDuplicateFlags)
                {
                    Svc.GameGui.OpenMapWithMapLink(m);
                }
            }
        }
        else
        {
            Svc.GameGui.OpenMapWithMapLink(m);
        }
    }

    private static void DecideWalkOrTeleport(MapLinkPayload m, Aetheryte nearestAetheryte)
    {
        var flagWorldPos = MapManager.GetFlagWorldPosition(m);
        if(flagWorldPos == null)
        {
            // can't compare distances, fall back to walking from where we are
            TaskMount.EnqueueIfEnabled();
            TaskMoveToFlag.EnqueueIfEnabled();
            TaskStopNearARank.EnqueueIfEnabled();
            return;
        }

        var walkDistance = Vector2.Distance(new(Player.Position.X, Player.Position.Z), new(flagWorldPos.Value.X, flagWorldPos.Value.Z));
        var aetherytePos = ECommons.GameHelpers.Map.AetherytePosition(nearestAetheryte);
        var teleportDistance = Vector2.Distance(new(aetherytePos.X, aetherytePos.Z), new(flagWorldPos.Value.X, flagWorldPos.Value.Z)) + P.Config.TeleportOverheadDistance;

        PluginLog.Debug($"Flag follow-up: walk={walkDistance:0.0}, teleport+walk={teleportDistance:0.0}");
        if(walkDistance <= teleportDistance)
        {
            TaskMount.EnqueueIfEnabled();
            TaskMoveToFlag.EnqueueIfEnabled();
            TaskStopNearARank.EnqueueIfEnabled();
        }
        else
        {
            TeleportTo(m, nearestAetheryte, 0);
        }
    }

    private static void TeleportTo(MapLinkPayload m, Aetheryte nearestAetheryte, int instance)
    {
        P.TeleportTo = ArrivalData.CreateOrNull(nearestAetheryte, m.TerritoryType.RowId, instance, isConductorTriggered: true);
        Utils.DelayTeleport();
        Notify.Info("Engaging Autoteleport");
    }
}
