using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.ExcelServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using HuntTrainAssistant.DataStructures;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTrainAssistant;
public static unsafe class Utils
{
    /// <summary>
    ///     Dismounting while flying just starts the character falling -- it isn't actually dismounted
    ///     until that fall finishes, so callers have to keep polling instead of firing-and-forgetting.
    /// </summary>
    public static bool TryDismount(string throttleKey)
    {
        if(!Svc.Condition[ConditionFlag.Mounted]) return true;

        if(Svc.Condition[ConditionFlag.MountOrOrnamentTransition] || IsUnmounting()) return false;

        if(!Player.IsAnimationLocked && EzThrottler.Throttle(throttleKey, 1000))
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

    public static string GetMountName(int id)
    {
        return Svc.Data.GetExcelSheet<Mount>().GetRow((uint)id).Singular.ExtractText();
    }

    public static void DelayTeleport()
		{
				if(P.Config.TeleportDelayEnabled && P.Config.TeleportDelayMax > 0 && P.Config.TeleportDelayMax >= P.Config.TeleportDelayMin)
				{
						var num = P.Config.TeleportDelayMin + Random.Shared.Next(P.Config.TeleportDelayMax - P.Config.TeleportDelayMin);
						if(EzThrottler.GetRemainingTime("Teleport") < num)
						{
								EzThrottler.Throttle("Teleport", num, true);
						}
				}
		}

		public static void DelayPathing()
		{
				if(P.Config.PathingDelayEnabled && P.Config.TeleportDelayMax > 0 && P.Config.TeleportDelayMax >= P.Config.TeleportDelayMin)
				{
						var num = P.Config.TeleportDelayMin + Random.Shared.Next(P.Config.TeleportDelayMax - P.Config.TeleportDelayMin);
						if(EzThrottler.GetRemainingTime("Pathing") < num)
						{
								EzThrottler.Throttle("Pathing", num, true);
						}
				}
		}

		public static bool CheckMultiMode()
		{
				if(S.AutoRetainerIPC.GetMultiModeStatus())
				{
						S.SonarMonitor.Continuation = null;
						P.TaskManager.Abort();
						P.Config.AutoVisitTeleportEnabled = false;
            PluginLog.Debug($"TeleportTo reset (4)");
            P.TeleportTo = null;
						return true;
        }
				else
				{
						return false;
				}
		}

		/// <param name="baseId">The NPC's <see cref="Dalamud.Game.ClientState.Objects.Types.IGameObject.BaseId"/> (BNpcBase row), not its name ID.</param>
		public static bool IsNpcIdInARankList(uint baseId)
		{
				if(P.Config.Debug) return true;
				// NotoriousMonster.Rank: 1 = B-rank, 2 = A-rank, 3 = S-rank. Sourced from the sheet instead of a
				// hardcoded per-expansion NPC ID list so new expansions' A-ranks work without a plugin update.
				return Svc.Data.GetExcelSheet<NotoriousMonster>().Any(x => x.BNpcBase.RowId == baseId && x.Rank == 2);
    }

		public static bool IsInHuntingTerritory()
		{
				if (ExcelTerritoryHelper.Get(Svc.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId == (int)TerritoryIntendedUseEnum.Open_World) return true;
        if (Svc.ClientState.TerritoryType.EqualsAny<uint>(
            1024, //mare <-> garlemard gateway
						682, 739, 759, //doman enclave
						635, 659 //rhalgr's reach
            )) return true; 
        if (Svc.ClientState.TerritoryType == MainCities.Idyllshire) return true;
				return false;
		}

		public static bool CanAutoInstanceSwitch()
		{
				if(P.KilledARanks.Count >= 2) return true;
				if(P.KilledARanks.Count == 1)
				{
						return Svc.Condition[ConditionFlag.InCombat] && Svc.Objects.OfType<IBattleNpc>().Any(x => Utils.IsNpcIdInARankList(x.BaseId) && (float)x.CurrentHp / (float)x.MaxHp < 0.5f && !x.IsDead);
				}
				return false;
    }
}
