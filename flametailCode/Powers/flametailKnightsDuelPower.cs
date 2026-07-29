using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 骑士对决：当场上只有 1 名存活的敌人时，你的反制牌可以免费打出。
/// </summary>
[RegisterPower]
public sealed class flametailKnightsDuelPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailCounterManagerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldMakeFree(card))
        {
            return false;
        }
        modifiedCost = default(decimal);
        return true;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldMakeFree(card))
        {
            return false;
        }
        modifiedCost = default(decimal);
        return true;
    }

    private bool ShouldMakeFree(CardModel card)
    {
        if (card.Owner.Creature != Owner)
        {
            return false;
        }

        switch (card.Pile?.Type)
        {
            case PileType.Hand:
            case PileType.Play:
                break;
            default:
                return false;
        }

        if (card is not ICounterCard)
        {
            return false;
        }

        ICombatState? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return false;
        }

        return combatState.Enemies.Count((Creature e) => e.IsAlive) == 1;
    }
}
