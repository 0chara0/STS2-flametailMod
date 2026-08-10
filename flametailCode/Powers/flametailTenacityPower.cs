using System.Threading.Tasks;
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
public sealed class flametailTenacityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailTenacityPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailTenacityPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner)
        {
            return;
        }

        if (card.Type != CardType.Curse && card.Type != CardType.Status)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }

    /// <summary>
    /// 基础效果：诅咒/状态牌对你造成的伤害与失去生命减半（不叠加）。
    ///
    /// 减伤走 <see cref="ModifyDamageMultiplicative"/> 而非 <see cref="ModifyHpLostBeforeOsty"/>：
    /// 伤害在格挡阶段前被修正，未被格挡的部分即成为失去生命的数值，因此单个钩子即可同时覆盖
    /// “伤害”和“失去生命”两种来源，避免二次减半。
    ///
    /// 固定返回 0.5 倍且不随 <see cref="Amount"/> 增长；Counter 叠加会合并为同一实例，
    /// 多次打出坚韧不拔只会提升抽到诅咒/状态牌时的格挡，不会重复减半。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner)
        {
            return 1m;
        }

        if (cardSource == null)
        {
            return 1m;
        }

        if (cardSource.Type != CardType.Curse && cardSource.Type != CardType.Status)
        {
            return 1m;
        }

        return 0.5m;
    }
}
