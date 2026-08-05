using ECommons.Automation;
using ECommons.CSExtensions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace HuntTrainAssistant.Tasks;
public static unsafe class TaskMoveToFlag
{
    public static void EnqueueIfEnabled()
    {
        if(P.Config.UseMoveToFlag)
        {
            P.TaskManager.Enqueue(() => IsScreenReady() && Player.Interactable, "Wait for player");
            P.TaskManager.Enqueue(() =>
            {
                if(AgentMap.Instance()->IsFlagMarkerSet)
                    Chat.ExecuteCommand("/vnav moveflag");
            }, "Move to flag");
        }
    }
}
