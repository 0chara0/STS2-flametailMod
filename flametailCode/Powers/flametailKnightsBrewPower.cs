using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 骑士特酿：本场战斗中，你的所有牌获得反制。
/// 这是一个隐藏标记能力，由「骑士特酿」药水施加；
/// <see cref="flametailCounterManagerPower"/> 据此把手牌任意牌视作反制牌（自动打出与回合末保留）。
/// 战斗结束时随战斗销毁，无需特殊清理。
/// </summary>
[RegisterPower]
public sealed class flametailKnightsBrewPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;
}
