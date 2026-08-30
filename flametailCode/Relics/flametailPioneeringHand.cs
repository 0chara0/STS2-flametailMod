using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 开拓之手：每场战斗开始时（第1个玩家回合），获得 3 点能量，并将战斗抽牌堆中随机一张能力牌放入你的手牌（优先非固有）。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailPioneeringHand : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3)
    ];

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailPioneeringHand)}.png",
        // 描边图：遗物图鉴按所属角色池染主题色，需用独立轮廓剪影图（_r4 为选定的描边版本）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailPioneeringHand)}_r4.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailPioneeringHand)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        // 每场战斗第 1 个玩家回合（能量重置后）才触发，避免后续回合重复加能量。
        if (player.PlayerCombatState.TurnNumber != 1)
        {
            return;
        }

        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);

        // 从战斗内抽牌堆选取能力牌（这些牌属于战斗，移入手牌合法），优先非固有。
        var drawPile = PileType.Draw.GetPile(Owner);
        List<CardModel> powerCards = drawPile.Cards.Where(c => c.Type == CardType.Power).ToList();
        if (powerCards.Count == 0)
        {
            return;
        }

        List<CardModel> nonInnate = powerCards.Where(c => !c.Keywords.Contains(CardKeyword.Innate)).ToList();
        List<CardModel> pool = nonInnate.Count > 0 ? nonInnate : powerCards;

        CardModel chosen = Owner.RunState!.Rng.CombatCardSelection.NextItem(pool)!;
        await CardPileCmd.Add(chosen, PileType.Hand, CardPilePosition.Top);
    }
}
