using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 计出万全：回合结束时可以多保留 N 张反制牌。
/// </summary>
[RegisterPower]
public sealed class flametailForesightPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailForesightPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailForesightPower.png");
}
