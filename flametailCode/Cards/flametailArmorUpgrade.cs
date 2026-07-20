using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailArmorUpgrade : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    private int _platedArmorAmount = 1;
    private int _blockAmount = 2;

    [SavedProperty]
    public int PlatedArmorAmount
    {
        get => _platedArmorAmount;
        set
        {
            AssertMutable();
            _platedArmorAmount = value;
            DynamicVars["PlatedArmorAmount"].BaseValue = value;
        }
    }

    [SavedProperty]
    public int BlockAmount
    {
        get => _blockAmount;
        set
        {
            AssertMutable();
            _blockAmount = value;
            DynamicVars["BlockAmount"].BaseValue = value;
        }
    }

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailArmorUpgrade"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Retain, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("PlatedArmorAmount", PlatedArmorAmount),
        new IntVar("BlockAmount", BlockAmount)
    ];

    public flametailArmorUpgrade() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> options = new()
        {
            Owner.Creature.CombatState!.CreateCard<flametailArmorUpgradePlatedOption>(Owner),
            Owner.Creature.CombatState!.CreateCard<flametailArmorUpgradeBlockOption>(Owner),
        };

        CardModel? choice = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            options,
            Owner,
            canSkip: false);

        if (choice == null)
        {
            return;
        }

        if (choice is flametailArmorUpgradePlatedOption)
        {
            int amount = PlatedArmorAmount;
            await PowerCmd.Apply<PlatingPower>(
                choiceContext,
                Owner.Creature,
                amount,
                Owner.Creature,
                this);

            PlatedArmorAmount += 1;
            (DeckVersion as flametailArmorUpgrade)?.AddPlatedArmor(1);
        }
        else if (choice is flametailArmorUpgradeBlockOption)
        {
            int amount = BlockAmount;
            await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Unpowered, cardPlay);

            BlockAmount += 2;
            (DeckVersion as flametailArmorUpgrade)?.AddBlock(2);
        }
    }

    public void AddPlatedArmor(int amount)
    {
        PlatedArmorAmount += amount;
    }

    public void AddBlock(int amount)
    {
        BlockAmount += amount;
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
