using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 领悟（升级后）：免费效果持续到本场战斗结束，回合开始不清空标记。
/// 与未升级的 <see cref="flametailInsightPower"/> 是相互独立的能力，互不混合；同类之间正常叠加。
/// 共通逻辑见 <see cref="flametailInsightPowerBase"/>。
/// </summary>
[RegisterPower]
public sealed class flametailInsightUpgradedPower : flametailInsightPowerBase
{
    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailInsightPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailInsightPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    protected override bool OnlyLastsThisTurn => false;
}
