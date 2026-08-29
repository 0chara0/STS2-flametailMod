using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Potions;

/// <summary>
/// 骑士特酿：本场战斗中，你的所有牌获得反制。
/// 施加隐藏标记能力 <see cref="flametailKnightsBrewPower"/>，反制管理器据此把手牌任意牌视作反制牌处理。
/// </summary>
[RegisterPotion(typeof(flametailPotionPool))]
public sealed class flametailKnightsBrew : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    private static readonly PotionAssetProfile _assetProfile = new(
        ImagePath: $"{Entry.ResPath}/images/relics/flametailRelic.png",
        OutlinePath: $"{Entry.ResPath}/images/relics/flametailRelic.png");
    public override PotionAssetProfile AssetProfile => _assetProfile;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await PowerCmd.Apply<flametailKnightsBrewPower>(choiceContext, Owner.Creature, 1, Owner.Creature, null);
    }
}
