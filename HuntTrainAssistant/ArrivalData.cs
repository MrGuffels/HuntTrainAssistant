using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.MathHelpers;
using HuntTrainAssistant.DataStructures;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTrainAssistant;
public class ArrivalData
{
    public readonly Aetheryte Aetheryte;
    public readonly Number Territory;
    public readonly Number Instance;
    public string World { get; init; }
    public bool IsConductorTriggered { get; init; }
    public Rank Rank { get; init; } = Rank.Unknown;
    public MapLinkPayload Link { get; init; }
    /// <summary>The chat message text that caused this flag/teleport, so it can be logged again at every point movement actually gets queued.</summary>
    public string SourceMessage { get; init; }

    public ArrivalData(Aetheryte aetheryte, Number territory, Number instance)
    {
        Aetheryte = aetheryte;
        Territory = territory;
        Instance = instance;
    }

    public static ArrivalData CreateOrNull(Number aetheryte, Number territory, Number instance, bool isConductorTriggered = false, Rank rank = Rank.Unknown, MapLinkPayload link = null, string sourceMessage = null)
    {
        if(Svc.Data.GetExcelSheet<Aetheryte>().TryGetRow(aetheryte, out var sheet))
        {
            return new(sheet, territory, instance) { IsConductorTriggered = isConductorTriggered, Rank = rank, Link = link, SourceMessage = sourceMessage };
        }
        return null;
    }

    public static ArrivalData CreateOrNull(Aetheryte? aetheryte, Number territory, Number instance, bool isConductorTriggered = false, Rank rank = Rank.Unknown, MapLinkPayload link = null, string sourceMessage = null)
    {
        if(aetheryte != null)
        {
            return new(aetheryte.Value, territory, instance) { IsConductorTriggered = isConductorTriggered, Rank = rank, Link = link, SourceMessage = sourceMessage };
        }
        return null;
    }
}
