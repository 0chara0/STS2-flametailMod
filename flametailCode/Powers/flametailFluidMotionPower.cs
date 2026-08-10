using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using flametail.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 隐藏 Buff：获得步法时把「进退自如」返回手牌。
/// </summary>
[RegisterPower]
public sealed class flametailFluidMotionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailFluidMotionPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailFluidMotionPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not flametailFootworkPower || amount <= 0 || Owner == null)
        {
            return;
        }

        // 多人模式下，只有「自己」获得步法时才把进退自如移回手牌；
        // 其他玩家获得步法触发的事件也会回调到这里，需按能力所有者过滤。
        if (power.Owner != Owner)
        {
            return;
        }

        var state = Owner.Player?.PlayerCombatState;
        if (state == null)
        {
            return;
        }

        // 先把匹配的牌快照出来，再逐个移回手牌。
        // 直接在 foreach 里调用 CardPileCmd.Add 会修改牌堆，导致枚举器失效并抛出异常，
        // 这会中断后续命令链，使触发步法的源牌无法进入弃牌堆，同时只有第一张牌成功移动。
        List<CardModel> fluidMotionCards = new List<CardModel>();
        fluidMotionCards.AddRange(state.DrawPile.Cards.Where(c => c is flametailFluidMotion));
        fluidMotionCards.AddRange(state.DiscardPile.Cards.Where(c => c is flametailFluidMotion));
        fluidMotionCards.AddRange(state.ExhaustPile.Cards.Where(c => c is flametailFluidMotion));

        foreach (CardModel card in fluidMotionCards)
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top);
        }
    }
}
