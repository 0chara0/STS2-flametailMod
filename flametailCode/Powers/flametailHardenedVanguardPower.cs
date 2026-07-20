using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 百战先锋：每当你反制时，额外执行一次该反制效果。
/// </summary>
[RegisterPower]
public sealed class flametailHardenedVanguardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailHardenedVanguardPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CounterSystem.IsCounterPlay)
        {
            return;
        }

        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        if (CounterSystem.IsHardenedVanguardReplication)
        {
            return;
        }

        if (cardPlay.Card.Pile?.Type == PileType.Exhaust)
        {
            return;
        }

        CounterSystem.IsHardenedVanguardReplication = true;
        try
        {
            // 把反制牌移回手牌再自动打出一次。
            if (cardPlay.Card.Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(cardPlay.Card, PileType.Hand, CardPilePosition.Top, this);
            }

            await CardCmd.AutoPlay(choiceContext, cardPlay.Card, CounterSystem.CurrentAttacker, AutoPlayType.Default);
        }
        finally
        {
            CounterSystem.IsHardenedVanguardReplication = false;
        }
    }
}
