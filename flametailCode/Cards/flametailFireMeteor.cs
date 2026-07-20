using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailFireMeteor : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{GetType().Name}.png");

    public flametailFireMeteor() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var dodgePower = Owner.Creature.GetPower<flametailDodgePower>();
        if (dodgePower == null || dodgePower.Amount < 2)
        {
            return;
        }

        await PowerCmd.ModifyAmount(choiceContext, dodgePower, -dodgePower.Amount, Owner.Creature, this);

        await PowerCmd.Apply<flametailFireMeteorExtraTurnPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);

        PlayerCmd.EndTurn(Owner, false, null);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
