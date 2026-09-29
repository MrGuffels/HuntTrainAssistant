using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using ECommons.CSExtensions;
using ECommons.DalamudServices.Legacy;
using Dalamud.Game.Chat;
using System.Text.RegularExpressions;

namespace HuntTrainAssistant;

internal unsafe static partial class ChatMessageHandler
{
    // Matches boss/conductor health-percent status broadcasts, e.g. "Chortitude: [99.2%]".
    // These still carry a map link to the conductor's current position, but they're periodic
    // status pings, not new flags -- reacting to them re-triggers movement for no reason.
    [GeneratedRegex(@"\[\d{1,3}(\.\d+)?%\]")]
    private static partial Regex HealthPercentRegex();

    // Guards against the same flag re-triggering the move task twice in quick succession -- seen live
    // with a conductor's identical message appearing on two channels at once (still unclear whether
    // that's dual-channel posting or duplicate delivery of a single message; [FlagTrace] logging below
    // is there to nail that down next time). Coordinate-based rather than raw text so two conductors
    // flagging the same spot moments apart still both go through.
    private static (uint Territory, float X, float Y)? _lastFiredFlag;
    private static DateTime _lastFiredFlagTime;
    private static readonly TimeSpan DuplicateFlagWindow = TimeSpan.FromSeconds(3);

    internal static void Chat_ChatMessage(IHandleableChatMessage cm)
    {
        var conductorNames = P.Config.Conductors.Select(x => x.Name).ToList();
        if (Svc.ClientState.LocalPlayer != null && P.Config.Enabled && ((cm.LogKind.EqualsAny(XivChatType.Shout, XivChatType.Yell, XivChatType.Say, XivChatType.CustomEmote, XivChatType.StandardEmote, XivChatType.Echo) && Utils.IsInHuntingTerritory()) || P.Config.Debug))
        {
            var isMapLink = false;
            var isConductorMessage = (P.Config.Debug && (cm.Sender.ToString().Contains(Svc.ClientState.LocalPlayer.Name.ToString()) || cm.LogKind == XivChatType.Echo)) || (TryDecodeSender(cm.Sender, out var s) && conductorNames.Contains(s.Name));
            //InternalLog.Debug($"Message: {message.ToString()} from {sender}, isConductor = {isConductorMessage}");
            foreach (var x in cm.Message.Payloads)
            {
                if (x is MapLinkPayload m)
                {
                    isMapLink = true;
                    // Conductors commonly post the same flag to both Yell and Shout -- reacting to both
                    // fires the flag twice (double abort/restart of the move task). Shout is the channel
                    // conductors actually rely on for train flags, so only act on that one.
                    var isTriggerChannel = cm.LogKind == XivChatType.Shout || (P.Config.Debug && cm.LogKind == XivChatType.Echo);
                    var isHealthPercent = HealthPercentRegex().IsMatch(cm.Message.TextValue);
                    (uint Territory, float X, float Y) flagKey = (m.TerritoryType.RowId, m.RawX / 1000f, m.RawY / 1000f);
                    var isDuplicate = _lastFiredFlag is { } last && last == flagKey && DateTime.UtcNow - _lastFiredFlagTime < DuplicateFlagWindow;
                    var willTrigger = isConductorMessage && isTriggerChannel && (Utils.IsInHuntingTerritory() || P.Config.Debug) && !isHealthPercent && !isDuplicate;
                    PluginLog.Information($"[FlagTrace] chan={cm.LogKind} sender={cm.Sender} text=\"{cm.Message.TextValue}\" link=({flagKey.X:0.00},{flagKey.Y:0.00})@{flagKey.Territory} isConductor={isConductorMessage} isTriggerChannel={isTriggerChannel} isHealthPercent={isHealthPercent} isDuplicate={isDuplicate} => {(willTrigger ? "FIRING OnConductorFlag" : "skipped")}");
                    if (willTrigger)
                    {
                        _lastFiredFlag = flagKey;
                        _lastFiredFlagTime = DateTime.UtcNow;
                        ConductorFlagHandler.OnConductorFlag(m, cm.Message.TextValue);
                    }
                    break;
                }
            }
            if (P.Config.SuppressChatOtherPlayers && !isMapLink && !isConductorMessage && conductorNames.Count > 0)
            {
                cm.PreventOriginal();
            }
            if (isConductorMessage)
            {
                var msg = new SeStringBuilder();
                msg.AddUiForeground(578);
                foreach (var x in cm.Message.Payloads)
                {
                    msg.Add(x);
                }
                msg.AddUiForegroundOff();
                cm.Message = msg.Build();

                if(P.Config.AudioAlert)
                {
                    if(Framework.Instance()->WindowInactive || !P.Config.AudioAlertOnlyMinimized)
                    {
                        if(EzThrottler.Throttle("AudioPlay", P.Config.AudioThrottle))
                        {
                            S.Notificator.PlaySound(P.Config.AudioAlertPath, P.Config.AudioAlertVolume, false, P.Config.AudioAlertOnlyMinimized);
                        }
                    }
                }
                if(Framework.Instance()->WindowInactive || P.Config.Debug)
                {
                    if(P.Config.TrayNotification)
                    {
                        S.Notificator.DisplayTrayNotification("[HTA] Conductor's message", cm.Message.GetText());
                    }
                    if(P.Config.FlashTaskbar)
                    {
                        S.Notificator.FlashTaskbarIcon();
                    }
                }
                if(P.Config.ExecuteMacroOnFlag && isMapLink && P.Config.MacroIndex.InRange(0, RaptureMacroModule.Instance()->Shared.Length))
                {
                    new TickScheduler(() =>
                    {
                        var macro = RaptureMacroModule.Instance()->Shared[P.Config.MacroIndex];
                        if(macro.IsNotEmpty())
                        {
                            RaptureShellModule.Instance()->ExecuteMacro(&macro);
                        }
                        else
                        {
                            PluginLog.Warning("Selected macro was empty");
                        }
                    });
                }
            }
        }
    }
}
