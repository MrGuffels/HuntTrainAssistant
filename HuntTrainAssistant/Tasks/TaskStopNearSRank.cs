using ECommons.GameHelpers;

namespace HuntTrainAssistant.Tasks;

/// <summary>
///     Stops short of an S-rank instead of flying straight to it. Community practice is to gather and
///     wait before pulling an S-rank, so unlike <see cref="TaskStopNearARank"/> we can't chase a live,
///     targetable mob -- it usually isn't even spawned yet. Distance is measured against the flag
///     position captured at enqueue time instead.
/// </summary>
public static class TaskStopNearSRank
{
    public static void EnqueueIfEnabled(Vector3? targetPos)
    {
        if(P.Config.AutoMoveToSRank && targetPos is { } pos)
        {
            P.TaskManager.Enqueue(() => WaitUntilNearSRank(pos), "Wait until near S-rank", new(timeLimitMS: 120000));
            P.TaskManager.Enqueue(StopAndDismount, "Stop and dismount", new(timeLimitMS: 15000));
        }
    }

    private static bool WaitUntilNearSRank(Vector3 targetPos)
    {
        if(!IsScreenReady() || !Player.Interactable) return false;

        var planarDistance = Vector2.Distance(new(Player.Position.X, Player.Position.Z), new(targetPos.X, targetPos.Z));
        if(planarDistance <= P.Config.StopNearSRankDistance)
        {
            S.VNavmeshIPC.Stop();
            return true;
        }

        return false;
    }

    private static bool StopAndDismount() => Utils.TryDismount("StopNearSRankDismount");
}
