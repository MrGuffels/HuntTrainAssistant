using ECommons.MathHelpers;
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

    /// <summary>
    ///     True when this teleport was triggered by a conductor's flag, as opposed to Sonar/HuntAlerts or a
    ///     manual button click. Only conductor-triggered teleports should follow up with move-to-flag/
    ///     stop-near-A-rank -- those come from Sonar for arbitrary A/S ranks, not the hunt train's own flags.
    /// </summary>
    public bool IsConductorTriggered { get; init; }

    public ArrivalData(Aetheryte aetheryte, Number territory, Number instance)
    {
        Aetheryte = aetheryte;
        Territory = territory;
        Instance = instance;
    }

    public static ArrivalData CreateOrNull(Number aetheryte, Number territory, Number instance, bool isConductorTriggered = false)
    {
        if(Svc.Data.GetExcelSheet<Aetheryte>().TryGetRow(aetheryte, out var sheet))
        {
            return new(sheet, territory, instance) { IsConductorTriggered = isConductorTriggered };
        }
        return null;
    }

    public static ArrivalData CreateOrNull(Aetheryte? aetheryte, Number territory, Number instance, bool isConductorTriggered = false)
    {
        if(aetheryte != null)
        {
            return new(aetheryte.Value, territory, instance) { IsConductorTriggered = isConductorTriggered };
        }
        return null;
    }
}
