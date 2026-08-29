using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailSkillOverStrength : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.AnyEnemy;
    private const bool ShowInCardLibrary = true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailSkillOverStrength"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Computed(
            "FootworkLoss", 0m,
            (card, target) => ResolveLoss(card),
            null),
        new IntVar("Multiplier", 2)
    ];

    public flametailSkillOverStrength() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    /// <summary>
    /// 实时计算本回合目标失去的力量：当前步法 × 倍率。
    /// 卡牌图书馆等非战斗场景渲染的是规范模型，访问 Owner 会抛异常，按 0 显示（配合描述条件在图书馆隐藏该行）。
    /// </summary>
    private static decimal ResolveLoss(CardModel? card)
    {
        if (card?.CombatState == null)
        {
            return 0m;
        }

        decimal multiplier = card.DynamicVars?.GetValueOrDefault("Multiplier", 2m) ?? 2m;
        decimal footwork = card.Owner?.Creature?.GetPower<flametailFootworkPower>()?.Amount ?? 0m;
        return footwork * multiplier;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        int loss = (int)DynamicVars.ComputeDynamicValue("FootworkLoss", 0m, cardPlay.Target);
        if (loss <= 0)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            cardPlay.Target,
            -loss,
            Owner.Creature,
            this);

        await PowerCmd.Apply<flametailRestoreStrengthPower>(
            choiceContext,
            cardPlay.Target,
            loss,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Multiplier"].UpgradeValueBy(1);
    }
}
