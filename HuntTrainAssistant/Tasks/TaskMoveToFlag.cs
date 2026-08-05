using ECommons.Automation;
using ECommons.CSExtensions;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace HuntTrainAssistant.Tasks;
public static unsafe class TaskMoveToFlag
{
    public static void EnqueueIfEnabled()
    {
        if(P.Config.UseMoveToFlag)
        {
            Utils.DelayPathing();
            P.TaskManager.Enqueue(() => IsScreenReady() && Player.Interactable, "Wait for player");
            P.TaskManager.Enqueue(() => EzThrottler.Check("Pathing"), "Wait for pathing delay");
            P.TaskManager.Enqueue(() =>
            {
                if(AgentMap.Instance()->IsFlagMarkerSet)
                    Chat.ExecuteCommand("/vnav flyflag");
            }, "Move to flag");
        }
    }
}
