using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Linq;

namespace HuntTrainAssistant.Tasks;
public static unsafe class TaskStopNearARank
{
    public static void EnqueueIfEnabled()
    {
        if(P.Config.StopNearARankEnabled)
        {
            P.TaskManager.Enqueue(WaitUntilNearARank, "Wait until near A-rank", new(timeLimitMS: 120000));
            P.TaskManager.Enqueue(TargetNearestARank, "Target A-rank", new(timeLimitMS: 15000));
            P.TaskManager.Enqueue(StopAndDismount, "Stop and dismount", new(timeLimitMS: 15000));
        }
    }

    /// <summary>
    ///     While the flag-chase is still en route, redirect toward the A-rank's live position once it's
    ///     targetable -- the flag can be stale/off from where the mob actually spawned or wandered to.
    ///     Distance is measured past the mob's hitbox edge (planar) rather than raw 3D distance so the
    ///     stop range scales with mob size instead of one flat number that over/undershoots.
    /// </summary>
    private static bool WaitUntilNearARank()
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var nearest = Svc.Objects
            .OfType<IBattleNpc>()
            .Where(x => x.IsTargetable && Utils.IsNpcIdInARankList(x.NameId))
            .OrderBy(x => Vector3.Distance(Player.Position, x.Position))
            .FirstOrDefault();

        if(nearest == null) return false;

        var planarDistance = Vector2.Distance(new(Player.Position.X, Player.Position.Z), new(nearest.Position.X, nearest.Position.Z)) - nearest.HitboxRadius;

        if(planarDistance <= P.Config.StopNearARankDistance)
        {
            Chat.ExecuteCommand("/vnav stop");
            return true;
        }

        if(EzThrottler.Throttle("StopNearARankChaseLiveTarget", 1500))
        {
            Chat.ExecuteCommand($"/vnav flyto {nearest.Position.X} {nearest.Position.Y} {nearest.Position.Z}");
        }

        return false;
    }

    /// <summary>
    ///     The external auto-battle system only engages once we actually have a target -- stopping and
    ///     dismounting near the A-rank isn't enough on its own.
    /// </summary>
    private static bool TargetNearestARank()
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var nearest = Svc.Objects
            .OfType<IBattleNpc>()
            .Where(x => x.IsTargetable && Utils.IsNpcIdInARankList(x.NameId))
            .OrderBy(x => Vector3.Distance(Player.Position, x.Position))
            .FirstOrDefault();
        if(nearest == null) return false;

        if(Svc.Targets.Target?.GameObjectId == nearest.GameObjectId) return true;

        if(EzThrottler.Throttle("StopNearARankSetTarget", 500))
            Svc.Targets.Target = nearest;

        return false;
    }
    /// <summary>
    ///     Dismounting while flying just starts the character falling -- it isn't actually dismounted
    ///     until that fall finishes, so we have to keep polling instead of firing-and-forgetting.
    /// </summary>
    private static bool StopAndDismount()
    {
        if(!Svc.Condition[ConditionFlag.Mounted]) return true;

        // still playing out the dismount/fall animation from a previous press
        if(Svc.Condition[ConditionFlag.MountOrOrnamentTransition] || IsUnmounting()) return false;

        if(!Player.IsAnimationLocked && EzThrottler.Throttle("StopNearARankDismount", 1000))
        {
            Chat.ExecuteGeneralAction(23);
            if(P.Config.DismountGraceEnabled)
                EzThrottler.Throttle("DismountGrace", P.Config.DismountGraceDuration, true);
        }

        return false;
    }

    private static bool IsUnmounting()
    {
        BattleChara* battleChara = (BattleChara*)(Svc.Objects[0]?.Address ?? 0);
        return battleChara != null && (battleChara->Mount.Flags & 1) == 1;
    }
}
