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

namespace HuntTrainAssistant;

internal unsafe static class ChatMessageHandler
{
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
                    if (isConductorMessage && (Utils.IsInHuntingTerritory() || P.Config.Debug))
                    {
                        ConductorFlagHandler.OnConductorFlag(m);
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
