using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Potions;

/// <summary>
/// 加速灵药：获得 1 点敏捷和 2 层闪避。
/// </summary>
[RegisterPotion(typeof(flametailPotionPool))]
public sealed class flametailSwiftElixir : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    private static readonly PotionAssetProfile _assetProfile = new(
        ImagePath: $"{Entry.ResPath}/images/relics/flametailRelic.png",
        OutlinePath: $"{Entry.ResPath}/images/relics/flametailRelic.png");
    public override PotionAssetProfile AssetProfile => _assetProfile;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1, Owner.Creature, null);
        await PowerCmd.Apply<flametailDodgePower>(choiceContext, Owner.Creature, 2, Owner.Creature, null);
    }
}
