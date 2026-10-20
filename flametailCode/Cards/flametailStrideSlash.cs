using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 踏步斩（重做，修改文档 [7][8]）：造成 4(5) 点伤害；
/// 本回合你每获得过一次格挡，伤害增加 3(4) 点。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailStrideSlash : ModCardTemplate
{
    private const int BaseEnergyCost = 0;
    private const CardType CardKind = CardType.Attack;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private const decimal BaseDamage = 4m;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailStrideSlash"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 注意：不能用名为 "Damage" 的 ComputedDynamicVar——引擎 DynamicVarSet.Damage
        // 会把该名字的变量强转为 DamageVar（卡图鉴构建升级版时抛 InvalidCastException，
        // InitGrid 任务崩掉 → 卡牌图鉴白屏）。
        // 修改清单 [5]：预览工厂里过一遍 Hook.ModifyDamage，让卡面数字也吃力量/易伤等修正
        // （实际伤害由 DamageCmd.Attack 的伤害管线统一应用，两处口径一致）。
        ModCardVars.Computed(
            "StrideDamage", BaseDamage,
            (card, target) => ResolveDamage(card),
            (card, previewMode, target, runGlobalHooks) =>
            {
                decimal value = ResolveDamage(card);
                if (!runGlobalHooks || card?.Owner == null)
                {
                    return value;
                }

                return Hook.ModifyDamage(
                    card.Owner.RunState,
                    card.CombatState,
                    target,
                    card.Owner.Creature,
                    value,
                    ValueProp.Move,
                    card,
                    null,
                    ModifyDamageHookType.All,
                    previewMode,
                    out _);
            }),
        new IntVar("BlockGainBonus", 3)
    ];

    // ⚠ 不设 GainsBlock：本牌是攻击牌、按本回合获得格挡的次数加伤，自身不给格挡，
    // true 会被灵巧附魔错误附魔出假显示（见 flametailWindbringer 注释）。
    public flametailStrideSlash() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 实时计算最终伤害：基础伤害 + 本回合获得格挡次数 × 每次加成。
    /// 卡牌图书馆等非战斗场景渲染的是规范模型，访问 Owner 会抛异常，按基础值回退。
    /// </summary>
    private static decimal ResolveDamage(CardModel? card)
    {
        // 未升级按 4 算、升级按 5 算（不能走 UpgradeValueBy：该变量是 ComputedDynamicVar，
        // 引擎 DynamicVarSet.Damage 强转 DamageVar 会在卡图鉴崩溃）。
        decimal baseDamage = card is { IsUpgraded: true } ? BaseDamage + 1 : BaseDamage;
        if (card?.CombatState == null)
        {
            return baseDamage;
        }

        decimal bonus = card.DynamicVars?.GetValueOrDefault("BlockGainBonus", 3m) ?? 3m;
        int blockCount = card.Owner?.Creature is { } creature
            ? CounterSystem.GetBlocksGainedThisTurn(creature)
            : 0;
        return baseDamage + bonus * blockCount;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        decimal totalDamage = DynamicVars.ComputeDynamicValue("StrideDamage", BaseDamage, cardPlay.Target);

        await DamageCmd.Attack(totalDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockGainBonus"].UpgradeValueBy(1);   // 3→4
    }
}
