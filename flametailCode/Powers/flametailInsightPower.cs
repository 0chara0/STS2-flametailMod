using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 领悟共通逻辑（抽象基类，不注册）：
/// 你每生成一张牌，手牌中随机 <see cref="Amount"/>（层数）张"有费用"的牌的耗能变为 0。
/// <para>
/// 生成事件使用原版能力 hook <see cref="AbstractModel.AfterCardGeneratedForCombat"/>
/// （CardPileCmd.AddGeneratedCardToCombat 底层触发，与原版 Arsenal“每生成一张牌获得 1 力量”同源）。
/// “耗能变为 0”通过 <see cref="TryModifyEnergyCostInCombatLate"/> / <see cref="TryModifyStarCost"/>
/// 对被标记卡牌返回 0 实现（参考 flametailReflexMovePower）。
/// </para>
/// <para>
/// 未升级（<see cref="flametailInsightPower"/>）与升级后（<see cref="flametailInsightUpgradedPower"/>）
/// 是两个独立注册的能力，同类之间正常叠加（层数 = 每次生成事件标记的牌数），不同类互不混合：
/// 未升级实例仅本回合有效（回合开始清空标记），升级后实例持续到本场战斗结束。
/// 同一实例在同一次生成事件中不会重复选中同一张牌。
/// </para>
/// </summary>
public abstract class flametailInsightPowerBase : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>免费效果是否只持续到本回合结束（未升级实例 true，升级后实例 false）。</summary>
    protected abstract bool OnlyLastsThisTurn { get; }

    /// <summary>被本实例标记为 0 费的卡牌集合（瞬态，实例私有）。</summary>
    private readonly HashSet<CardModel> _freeCards = new();

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只响应自己一方生成的牌。
        if (creator == null || creator.Creature != Owner)
        {
            return;
        }

        var hand = creator.PlayerCombatState?.Hand;
        if (hand == null || hand.Cards.Count == 0)
        {
            return;
        }

        // 只在“有费用”的牌中挑选：排除 X 费与无费用（不可打出）的牌；
        // 当前结算费用已为 0 的牌标记后也没有变化，同样排除。
        var pool = new List<CardModel>();
        foreach (CardModel handCard in hand.Cards)
        {
            if (!handCard.EnergyCost.CostsX && handCard.EnergyCost.GetResolved() > 0)
            {
                pool.Add(handCard);
            }
        }

        // 每次生成事件：从候选中随机抽 min(Amount, 候选数) 张。
        int count = Math.Min((int)Amount, pool.Count);
        if (count <= 0)
        {
            return;
        }

        // 随机使用战斗卡牌选择随机流（参考 flametailHandguardDefend / flametailFaceOff）。
        // 每选中一张就将其移出候选池，保证同一实例在同一次生成事件中不重复选中同一张牌。
        bool marked = false;
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            CardModel? picked = creator.RunState!.Rng.CombatCardSelection.NextItem(pool);
            if (picked == null)
            {
                break;
            }

            _freeCards.Add(picked);
            pool.Remove(picked);
            marked = true;
        }

        // 有标记时闪烁能力图标，提供明确的触发反馈（与原版 Arsenal 一致）。
        if (marked)
        {
            Flash();
        }

        await Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!_freeCards.Contains(card))
        {
            return false;
        }

        modifiedCost = default(decimal);
        return true;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!_freeCards.Contains(card))
        {
            return false;
        }

        modifiedCost = default(decimal);
        return true;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (OnlyLastsThisTurn && participants.Contains(Owner))
        {
            // 仅本回合有效的实例：回合开始时清空上一回合的标记。
            _freeCards.Clear();
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 领悟（未升级）：免费效果仅本回合有效，回合开始清空标记。
/// </summary>
[RegisterPower]
public sealed class flametailInsightPower : flametailInsightPowerBase
{
    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailInsightPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailInsightPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    protected override bool OnlyLastsThisTurn => true;
}
