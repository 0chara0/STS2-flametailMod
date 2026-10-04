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
/// 长夜需尽：每回合开始时，向你手牌添加 [Amount]（层数）张[睡眠不佳]（原版状态牌 <see cref="PoorSleep"/>），
/// 抽 [Amount] 张牌，然后由你选择消耗 [Amount] 张手牌，获得 [Amount] 点能量。
/// 所有数值均随层数叠加（修改文档 [42]）。
/// 消耗选择与原版暴政 TyrannyPower 一致：<c>CardSelectCmd.FromHand</c> + <c>ExhaustSelectionPrompt</c>，
/// 经 <c>CardCmd.Exhaust</c> 正常走消耗流程（触发“消耗时”钩子）。
/// <para>
/// 回合开始 hook 使用 <see cref="AbstractModel.AfterPlayerTurnStartEarly"/>。
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

        int n = (int)Amount;   // 层数：抽牌/能量/消耗/生成的睡眠不佳全部随层数叠加。

        // 1) 向手牌添加 n 张[睡眠不佳]。
        for (int i = 0; i < n; i++)
        {
            CardModel poorSleep = Owner.CombatState!.CreateCard<PoorSleep>(player);
            await CardPileCmd.AddGeneratedCardToCombat(poorSleep, PileType.Hand, player);
        }

        // 2) 抽 n 张牌。
        if (n > 0)
        {
            await CardPileCmd.Draw(choiceContext, n, player);
        }

        // 3) 由你选择消耗 n 张手牌（一次选择流程；手牌为空/不足时 FromHand 自然返回可行结果）。
        foreach (CardModel card in await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, n, n),
            null,
            this))
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 4) 获得 n 点能量。
        await PlayerCmd.GainEnergy(n, player);
    }
}
