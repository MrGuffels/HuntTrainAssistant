using ECommons.Automation;
using ECommons.CSExtensions;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace HuntTrainAssistant.Tasks;
public static unsafe class TaskMoveToFlag
{
    /// <param name="worldPos">
    ///     Captured destination to fly to. Preferred over reading the live map flag marker at
    ///     execution time -- that marker can be overwritten in the gap between enqueueing and
    ///     running (e.g. Sonar or another player flagging a different spot on the map), which would
    ///     otherwise send us chasing the wrong location entirely.
    /// </param>
    public static void EnqueueIfEnabled(Vector3? worldPos = null)
    {
        if(P.Config.UseMoveToFlag)
        {
            Utils.DelayPathing();
            P.TaskManager.Enqueue(() => IsScreenReady() && Player.Interactable, "Wait for player");
            P.TaskManager.Enqueue(() => EzThrottler.Check("Pathing"), "Wait for pathing delay");
            P.TaskManager.Enqueue(() =>
            {
                if(worldPos is { } pos)
                {
                    // TryMoveTo no-ops (and we retry next tick) while vnavmesh is still computing a
                    // prior route -- issuing flyto on top of that would restart pathfinding from a
                    // stale position and cause a visible backtrack/jitter.
                    return S.VNavmeshIPC.TryMoveTo(pos, true);
                }
                if(AgentMap.Instance()->IsFlagMarkerSet)
                {
                    Chat.ExecuteCommand("/vnav flyflag");
                }
                return true;
            }, "Move to flag");
        }
    }
}
