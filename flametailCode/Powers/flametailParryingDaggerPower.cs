using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

[RegisterPower]
public sealed class flametailParryingDaggerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailParryingDaggerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailParryingDaggerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var counterContext = this.GetCounterContext();
        if (!counterContext.IsCounterPlay)
        {
            return;
        }

        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        if (Owner.CombatState is { } combat)
        {
            foreach (Creature target in CounterSystem.GetCounterTargets(counterContext.CurrentAttacker, combat))
            {
                if (target.IsDead)
                {
                    continue;
                }

                await DamageCmd.Attack(Amount)
                    .FromCard(cardPlay.Card, cardPlay)
                    .Targeting(target)
                    .Unpowered()
                    .Execute(choiceContext);
            }
        }

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
