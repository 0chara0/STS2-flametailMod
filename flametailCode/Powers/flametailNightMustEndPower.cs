using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 长夜需尽：每回合开始时，向你手牌添加一张[睡眠不佳]（原版状态牌 <see cref="PoorSleep"/>），
/// 抽 <see cref="Amount"/>（层数）张牌，然后由你选择消耗一张手牌（选牌页面与原版
/// 暴政 TyrannyPower 一致：<c>CardSelectCmd.FromHand</c> + <c>ExhaustSelectionPrompt</c>），
/// 获得 1 点能量。消耗经 <c>CardCmd.Exhaust</c> 正常走消耗流程（触发“消耗时”钩子）。
/// <para>
/// 回合开始 hook 使用 <see cref="AbstractModel.AfterPlayerTurnStartEarly"/>：它在起手抽牌之后、
/// 其它回合开始效果之前触发（CombatManager.SetupPlayerTurn 末尾经 Hook.AfterPlayerTurnStart 调用），
/// 且是官方文档建议的“需要在回合开始做玩家选择（如选牌）时”使用的 hook。
/// </para>
/// </summary>
[RegisterPower]
public sealed class flametailNightMustEndPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailNightMustEndPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailNightMustEndPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        // 1) 向手牌添加一张[睡眠不佳]。
        CardModel poorSleep = Owner.CombatState!.CreateCard<PoorSleep>(player);
        await CardPileCmd.AddGeneratedCardToCombat(poorSleep, PileType.Hand, player);

        // 2) 抽牌。
        int drawAmount = (int)Amount;
        if (drawAmount > 0)
        {
            await CardPileCmd.Draw(choiceContext, drawAmount, player);
        }

        // 3) 由你选择消耗一张手牌（与原版暴政 TyrannyPower 同一套 FromHand 选择页面；
        //    经 CardCmd.Exhaust 正常走消耗流程，触发“消耗时”钩子。手牌为空则无牌可选）。
        foreach (CardModel card in await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            null,
            this))
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 4) 获得 1 点能量。
        await PlayerCmd.GainEnergy(1, player);
    }
}
