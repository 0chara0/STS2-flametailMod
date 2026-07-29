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
/// 先发制人：在每个敌人行动之前，从手牌中自动打出一张满足条件的反制牌，如同受到了该敌人的攻击。
/// </summary>
[RegisterPower]
public sealed class flametailPreemptPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailForesightPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailForesightPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    /// <summary>
    /// 在指定敌人行动之前触发先发制人效果。
    /// </summary>
    public async Task OnBeforeEnemyAction(PlayerChoiceContext choiceContext, Player player, Creature enemy)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        if (player.Creature.IsDead || enemy.IsDead)
        {
            return;
        }

        CardPile? hand = player.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return;
        }

        for (int i = 0; i < Amount; i++)
        {
            if (enemy.IsDead)
            {
                return;
            }

            CardModel? counterCard = null;
            foreach (CardModel card in hand.Cards)
            {
                if (card is ICounterCard counter
                    && counter.CanAutoPlayAsCounter(enemy)
                    && counter.CanBePlayedBySupportEffects)
                {
                    counterCard = card;
                    break;
                }
            }

            if (counterCard == null)
            {
                return;
            }

            bool oldIsCounterPlay = CounterSystem.IsCounterPlay;
            Creature? oldAttacker = CounterSystem.CurrentAttacker;
            bool oldDodged = CounterSystem.WasAttackDodged;
            bool oldMitigated = CounterSystem.LastAttackMitigated;

            CounterSystem.IsCounterPlay = true;
            CounterSystem.CurrentAttacker = enemy;
            CounterSystem.WasAttackDodged = false;
            CounterSystem.LastAttackMitigated = false;

            try
            {
                await CounterSystem.PlayCounterCard(choiceContext, counterCard, enemy);
            }
            finally
            {
                CounterSystem.IsCounterPlay = oldIsCounterPlay;
                CounterSystem.CurrentAttacker = oldAttacker;
                CounterSystem.WasAttackDodged = oldDodged;
                CounterSystem.LastAttackMitigated = oldMitigated;
            }
        }
    }
}
