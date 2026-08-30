using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 携风-盟友：挂在“携风”选定的目标玩家身上，作为该（施放者, 盟友）连接的唯一事实来源。
/// InstancedPerApplier：同一施放者对同一盟友重复打出会叠加层数，不同施放者各自独立实例 —— 从不同盟友处获得的增益分开显示、分开结算。
/// Amount 即该连接的分享层数（本 power 上可见）；分享时由 flametailWindbringerPower 按各连接自己的层数分别结算。
/// 做法参照原版“肉盾”(Tank 卡给队友的 GuardedPower)：描述指明来源（施放者名），并在施放者死亡时自动移除。
/// </summary>
[RegisterPower]
public sealed class flametailWindbringerLinkPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new StringVar("Applier") };

    /// <summary>
    /// 施放者（携风持有者玩家）的 NetId，用于在其死亡时移除本印记。
    /// </summary>
    [SavedProperty]
    public string ApplierNetId { get; set; } = "";

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailWindbringerLinkPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailWindbringerLinkPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 记录施放者供 AfterDeath 判断，并把施放者名写入描述变量（指明来源，参照 GuardedPower）。
        // 用 Applier 属性而非入参，与 GuardedPower 完全一致。
        if (Applier?.Player != null)
        {
            ApplierNetId = Applier.Player.NetId.ToString();
            ((StringVar)DynamicVars["Applier"]).StringValue =
                PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, Applier.Player.NetId);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        // 施放者死亡 → 移除本印记，避免指示残留。
        if (wasRemovalPrevented)
        {
            return;
        }

        if (ulong.TryParse(ApplierNetId, out ulong applierNetId) && creature.Player?.NetId == applierNetId)
        {
            await PowerCmd.Remove(this);
        }
    }
}
