using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 《最后的骑士》：每当你被施加负面状态时，你获得 2 点力量。
/// 通过 AfterPowerAmountChanged 监听：当施加到自身的 power 按当前层数为 Debuff 且数量增加时触发。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailTheLastKnight : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Strength", 2)
    ];

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheLastKnight)}.png",
        // 描边图：遗物图鉴按所属角色池染主题色，需用独立轮廓剪影图（_r4 为选定的描边版本）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheLastKnight)}_r4.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailTheLastKnight)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power.Owner != Owner.Creature)
        {
            return;
        }

        if (power.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return;
        }

        if (amount <= 0m)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["Strength"].IntValue, Owner.Creature, null);
    }
}
