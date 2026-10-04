using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 招架匕首：每当一张牌将要反制打出时（每张，含反制链上的每一张），获得 Amount 点格挡
/// 并对该敌人造成 Amount 点伤害（修改文档 [26][28]：时机提前至反制打出前）。
/// </summary>
[RegisterPower]
public sealed class flametailParryingDaggerPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailParryingDaggerPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailParryingDaggerPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
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

        // 将要反制（每张反制牌打出前，与是否在链中无关）：先格挡后伤害。
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);

        if (counterContext.CurrentAttacker is { IsDead: false } attacker)
        {
            await DamageCmd.Attack(Amount)
                .FromCard(cardPlay.Card, cardPlay)
                .Targeting(attacker)
                .Unpowered()
                .Execute(null);
        }
    }
}
