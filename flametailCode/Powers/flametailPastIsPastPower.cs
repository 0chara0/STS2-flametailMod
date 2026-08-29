using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 已往不谏：当一张诅咒牌从你的手牌进入弃牌堆时，改为将其消耗。
/// 覆盖「直接弃牌（Hand→Discard）」与「回合结束打出（Hand→Play→Discard）」两条路径；
/// 不作用于由效果直接生成到弃牌堆（oldPile=None）或由「殊死一搏」返回弃牌堆的诅咒牌。
/// </summary>
[RegisterPower]
public sealed class flametailPastIsPastPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailPastIsPastPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailPastIsPastPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    /// <summary>本回合从手牌移出的诅咒牌（等待其进入弃牌堆后消耗）。</summary>
    private readonly HashSet<CardModel> _trackedCurses = new();

    public override async Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner.Creature != Owner)
        {
            return;
        }

        if (card.Type != CardType.Curse)
        {
            return;
        }

        // 从手牌移出的诅咒牌：记住它，等待进入弃牌堆。
        if (oldPileType == PileType.Hand)
        {
            _trackedCurses.Add(card);
        }

        // 记过的诅咒牌进入弃牌堆 → 改为消耗。
        if (card.Pile?.Type == PileType.Discard)
        {
            if (_trackedCurses.Remove(card))
            {
                await CardPileCmd.Add(card, PileType.Exhaust, CardPilePosition.Top, this);
            }
            return;
        }

        // 记过的诅咒牌去了其它非 Play 堆（如被消耗/移除），不再跟踪。
        if (_trackedCurses.Contains(card) && card.Pile != null && card.Pile.Type != PileType.Play)
        {
            _trackedCurses.Remove(card);
        }
    }
}
