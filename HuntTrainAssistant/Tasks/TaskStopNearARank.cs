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
            P.TaskManager.Enqueue(StopAndDismount, "Stop and dismount", new(timeLimitMS: 15000));
        }
    }

    private static bool WaitUntilNearARank()
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var nearestDistance = Svc.Objects
            .OfType<IBattleNpc>()
            .Where(x => x.IsTargetable && Utils.IsNpcIdInARankList(x.NameId))
            .Select(x => Vector3.Distance(Player.Position, x.Position))
            .DefaultIfEmpty(float.MaxValue)
            .Min();

        if(nearestDistance > P.Config.StopNearARankDistance) return false;

        Chat.ExecuteCommand("/vnav stop");
        return true;
    }

    private static bool StopAndDismount()
    {
        if(!Svc.Condition[ConditionFlag.Mounted]) return true;

        // still playing out the dismount/fall animation from a previous press
        if(Svc.Condition[ConditionFlag.MountOrOrnamentTransition] || IsUnmounting()) return false;

        if(!Player.IsAnimationLocked && EzThrottler.Throttle("StopNearARankDismount", 1000))
            Chat.ExecuteGeneralAction(23);

        return false;
    }

    private static bool IsUnmounting()
    {
        BattleChara* battleChara = (BattleChara*)(Svc.Objects[0]?.Address ?? 0);
        return battleChara != null && (battleChara->Mount.Flags & 1) == 1;
    }
}
