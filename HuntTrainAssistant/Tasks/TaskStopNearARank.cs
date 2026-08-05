using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.GameHelpers;
using System.Linq;

namespace HuntTrainAssistant.Tasks;
public static class TaskStopNearARank
{
    public static void EnqueueIfEnabled()
    {
        if(P.Config.StopNearARankEnabled)
        {
            P.TaskManager.Enqueue(() =>
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
                if(Svc.Condition[ConditionFlag.Mounted])
                    Chat.ExecuteGeneralAction(23);
                return true;
            }, "Stop near A-rank", new(timeLimitMS: 120000));
        }
    }
}
