using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using HuntTrainAssistant.DataStructures;

namespace HuntTrainAssistant.Tasks;

/// <summary>
///     Pings NotificationMaster once an S-rank teleport has actually landed (screen loaded, player
///     interactable, instance switch done if one was queued ahead of this), so an alt-tabbed player
///     knows to come back and get ready.
/// </summary>
public static unsafe class TaskNotifySRankArrival
{
    public static void EnqueueIfEnabled(ArrivalData arrival)
    {
        if(!P.Config.NotifySRankArrival || !arrival.Rank.EqualsAny(Rank.S, Rank.SS)) return;
        var rank = arrival.Rank;
        var zone = arrival.Aetheryte.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToString() ?? "unknown zone";
        var sourceMessage = arrival.SourceMessage;
        P.TaskManager.Enqueue(() => !S.LifestreamIPC.IsBusy() && IsScreenReady() && Player.Interactable, "Wait for S-rank arrival");
        P.TaskManager.Enqueue(() => Notify(rank, zone, sourceMessage), "Notify S-rank arrival");
    }

    private static void Notify(Rank rank, string zone, string sourceMessage)
    {
        PluginLog.Information($"[SRankArrival] Arrived for {rank} in {zone}, from message: \"{sourceMessage}\"");
        if(!Framework.Instance()->WindowInactive && !P.Config.Debug) return;
        var instance = S.LifestreamIPC.GetCurrentInstance();
        S.Notificator.DisplayTrayNotification($"[HTA] Arrived for {rank} rank", instance > 0 ? $"{zone} (instance {instance})" : zone);
        if(P.Config.FlashTaskbar) S.Notificator.FlashTaskbarIcon();
        if(P.Config.NotifySRankArrivalForeground && !S.Notificator.TryBringGameForeground())
        {
            PluginLog.Warning("[SRankArrival] NotificationMaster failed to bring game to foreground");
        }
    }
}
