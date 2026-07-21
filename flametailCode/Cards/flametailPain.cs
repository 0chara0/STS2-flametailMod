using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 诅咒牌：疼痛。在手牌中时，每打出一张牌失去 1 点生命（无视格挡）。
/// </summary>
[RegisterCard(typeof(CurseCardPool))]
public sealed class flametailPain : ModCardTemplate
{
    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{nameof(flametailPain)}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    public flametailPain()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只响应拥有者自己打出的牌
        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        // 只在手牌中时生效
        if (Pile?.Type != PileType.Hand)
        {
            return;
        }

        if (Owner.Creature.IsDead)
        {
            return;
        }

        // 无视格挡的 1 点伤害；走命令管道以支持多人同步与历史记录
        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            1,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            this,
            null);
    }
}
