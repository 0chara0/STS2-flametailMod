using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using flametail.Helpers;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 《光耀卡西米尔》：每当你打出一张反制牌时，对随机敌人造成 8 点固定伤害，
/// 不受力量（攻击方）与易伤（目标方）等伤害修正影响。
/// 主动打出与反制自动打出都会触发 AfterCardPlayed。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailGloriousKazimierz : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Damage", 6)
    ];

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailGloriousKazimierz)}.png",
        // 描边图：遗物图鉴按所属角色池染主题色，需用独立轮廓剪影图（_r4 为选定的描边版本）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailGloriousKazimierz)}_r4.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailGloriousKazimierz)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        if (cardPlay.Card is not ICounterCard)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars["Damage"].IntValue)
            .FromCard(cardPlay.Card, cardPlay)
            .TargetingRandomOpponents(Owner.Creature.CombatState!)
            .IgnoreAttackerModifiers()
            .IgnoreDefenderModifiers()
            .Execute(choiceContext);
    }
}
