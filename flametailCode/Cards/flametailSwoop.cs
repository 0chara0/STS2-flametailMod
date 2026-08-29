using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 飞扑：你受到 6 点伤害，你与另一名玩家各获得 1 层闪避。消耗。
/// 多人限定；另一名玩家通过 TargetType.AnyAlly 的“选择其他玩家”界面确定（排除自己）。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailSwoop : ModCardTemplate
{
    private const int BaseEnergyCost = 3;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyAlly;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailSwoop"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    // 仅多人模式可用的卡：需要选择另一名玩家作为共享对象，单人模式没有意义。
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    public flametailSwoop() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 自己受到 6 点不可格挡、不受力量影响的伤害（参考游戏内放血/自伤类牌的写法）。
        await CreatureCmd.Damage(choiceContext, Owner.Creature, 6, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this, cardPlay);

        await PowerCmd.Apply<flametailDodgePower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);

        // cardPlay.Target 由 TargetType.AnyAlly 的多人在线目标选择界面给出（排除自己）。
        if (cardPlay.Target != null)
        {
            await PowerCmd.Apply<flametailDodgePower>(
                choiceContext,
                cardPlay.Target,
                1,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
