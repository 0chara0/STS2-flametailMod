using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 闪！转！腾！挪！的「上一张是攻击牌」标记（2026-10-18 用户裁定的 BUFF 栏交互）：
/// 打出攻击牌后由 <see cref="flametailDashWeaveDodgePower"/> 挂上；
/// 你打出的下一张牌为技能牌时，由管理能力结算获得 1 点能量。
/// 纯状态标记，无监听逻辑——存在性即状态，天然随 power 存在性联机同步。
/// </summary>
[RegisterPower]
public sealed class flametailDashWeaveAttackMarkPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // 占位图标（步法图）；有正式素材后替换。
    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFootworkPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;
}

/// <summary>
/// 闪！转！腾！挪！的「上一张是技能牌」标记：
/// 打出技能牌后由 <see cref="flametailDashWeaveDodgePower"/> 挂上；
/// 你打出的下一张牌为攻击牌时，由管理能力结算抽 1 张牌。
/// </summary>
[RegisterPower]
public sealed class flametailDashWeaveSkillMarkPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // 占位图标（闪避图）；有正式素材后替换。
    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailDodgePower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailDodgePower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;
}
