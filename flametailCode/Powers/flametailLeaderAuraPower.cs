using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 领导者-盟友：挂在享受“领导者”光环的队友玩家身上，作为该（施放者, 盟友）连接的唯一事实来源。
/// InstancedPerApplier：同一施放者重复打出会叠加层数，不同施放者各自独立实例 —— 从不同盟友处获得的增益分开显示、分开结算。
/// 本光环自己管理对该盟友实际授予的力量/敏捷：血量跨过半血线（或死亡）时只更新自己这一份，互不影响。
/// 施放者死亡时自动移除，并精确撤销自己已授予的增益（不影响其他施放者的贡献）。
/// </summary>
[RegisterPower]
public sealed class flametailLeaderAuraPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new StringVar("Applier") };

    /// <summary>
    /// 施放者（领导者玩家）的 NetId，用于在其死亡时移除本光环。
    /// </summary>
    [SavedProperty]
    public string ApplierNetId { get; set; } = "";

    /// <summary>本连接当前实际授予的点数（撤销/重授时精确归还自己这一份）。</summary>
    [SavedProperty]
    public int GrantedAmount { get; set; }

    /// <summary>本连接当前授予的是力量(true)还是敏捷(false)。</summary>
    [SavedProperty]
    public bool GrantedIsStrength { get; set; }

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailLeaderAuraPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailLeaderAuraPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 记录施放者供 AfterDeath 判断，并把施放者名写入描述变量（指明来源，参照 GuardedPower）。
        // 用 Applier 属性而非入参，与 GuardedPower 完全一致。
        if (Applier?.Player != null)
        {
            ApplierNetId = Applier.Player.NetId.ToString();
            ((StringVar)DynamicVars["Applier"]).StringValue =
                PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, Applier.Player.NetId);
        }

        // 初次授予：按当前血量立刻生效（与 AfterPowerAmountChanged 的同步互为幂等，不会叠加）。
        await SyncGrant(new ThrowingPlayerChoiceContext());
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 本钩子对所有 power 变化广播，只在自己层数变化（同一施放者再次打出叠加）时重新同步实际授予。
        if (power != this)
        {
            return;
        }

        await SyncGrant(choiceContext);
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        // 只关心本光环宿主（盟友）自己的血量变化；其他生物的血量变化不触发。
        if (creature != Owner)
        {
            return;
        }

        if (Owner.CombatState == null)
        {
            return;
        }

        // 宿主死亡：移除本光环，AfterRemoved 会撤销已授予的增益。
        if (creature.IsDead)
        {
            await PowerCmd.Remove(this);
            return;
        }

        // 是否跨过半血线：比较变化前(CurrentHp - delta)与变化后(CurrentHp)相对半血线的位置。
        // 仅在真正跨线时才更新，避免每次掉血/回血都重授造成图标抖动。
        decimal beforeHp = creature.CurrentHp - delta;
        bool beforeAboveHalf = beforeHp * 2 > creature.MaxHp;
        bool nowAboveHalf = creature.CurrentHp * 2 > creature.MaxHp;
        if (beforeAboveHalf == nowAboveHalf)
        {
            return;
        }

        await SyncGrant(new ThrowingPlayerChoiceContext());
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        // 施放者（领导者）死亡 → 移除本光环（AfterRemoved 会撤销已授予的增益），避免增益残留。
        if (wasRemovalPrevented)
        {
            return;
        }

        if (ulong.TryParse(ApplierNetId, out ulong applierNetId) && creature.Player?.NetId == applierNetId)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        // 光环被移除时，精确撤销本连接已授予的力量/敏捷（不影响其他施放者的贡献）。
        await RemoveGranted(new ThrowingPlayerChoiceContext());
    }

    /// <summary>
    /// 撤销旧授予并按当前 Amount 与宿主血量重新授予（力量/敏捷），记录本连接当前实际授予值。
    /// 若已在同步状态则直接返回（幂等，避免同帧重复触发时叠加）。
    /// </summary>
    private async Task SyncGrant(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || Owner.IsDead || Owner.CombatState == null)
        {
            return;
        }

        bool aboveHalf = Owner.CurrentHp * 2 > Owner.MaxHp;
        if (GrantedAmount == (int)Amount && GrantedIsStrength == aboveHalf)
        {
            return;
        }

        await RemoveGranted(choiceContext);

        int grant = (int)Amount;
        if (grant <= 0)
        {
            return;
        }

        // 生命值高于一半 → 力量；不高于一半 → 敏捷。
        if (aboveHalf)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, grant, Applier, null);
        }
        else
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, grant, Applier, null);
        }

        GrantedAmount = grant;
        GrantedIsStrength = aboveHalf;
    }

    /// <summary>
    /// 精确撤销本连接已授予的点数（至多减到 0，不影响其他来源的贡献）。
    /// </summary>
    private async Task RemoveGranted(PlayerChoiceContext choiceContext)
    {
        if (GrantedAmount <= 0 || Owner == null)
        {
            return;
        }

        if (GrantedIsStrength && Owner.GetPower<StrengthPower>() is { } strength)
        {
            decimal offset = -System.Math.Min(strength.Amount, (decimal)GrantedAmount);
            await PowerCmd.ModifyAmount(choiceContext, strength, offset, Applier, null);
        }
        else if (!GrantedIsStrength && Owner.GetPower<DexterityPower>() is { } dexterity)
        {
            decimal offset = -System.Math.Min(dexterity.Amount, (decimal)GrantedAmount);
            await PowerCmd.ModifyAmount(choiceContext, dexterity, offset, Applier, null);
        }

        GrantedAmount = 0;
    }
}
