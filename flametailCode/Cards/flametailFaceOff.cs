using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 锋芒相对：获得 12（15）点格挡。从 3 张随机的反制牌中选择一张加入你的手牌，该牌在本回合被保留。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailFaceOff : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Uncommon;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailFaceOff"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12, ValueProp.Move)
    ];

    public flametailFaceOff() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.Creature?.CombatState is not { } combatState)
        {
            return;
        }

        // 从反制牌注册表随机取 3 个去重生成器，各造一张候选牌。
        var rng = Owner.RunState?.Rng.CombatCardSelection;
        if (rng == null)
        {
            return;
        }

        var creators = flametailCounterCardRegistry.Creators.ToList();
        List<CardModel> choices = new(3);
        while (choices.Count < 3 && creators.Count > 0)
        {
            var creator = rng.NextItem(creators);
            if (creator == null)
            {
                break;
            }

            creators.Remove(creator);
            // 注册表里是规范卡（无主），需用 CreateCard 转成属于 Owner 的可变卡才能加入战斗。
            choices.Add(combatState.CreateCard(creator(), Owner));
        }

        if (choices.Count == 0)
        {
            return;
        }

        // 三选一（不可跳过），获取的牌加入手牌并保留本回合。
        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            choices,
            Owner,
            canSkip: false);

        if (chosen == null)
        {
            return;
        }

        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
        chosen.GiveSingleTurnRetain();
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
