using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 红松的起点：每回合开始时获得 1 层步法。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
[RegisterCharacterStarterRelic(typeof(flametailCharacter))]
public sealed class PinusSylvestrisOrigins : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Footwork", 1)
    ];

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/flametailRelic.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/flametailRelic.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/flametailRelic.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        await PowerCmd.Apply<flametailFootworkPower>(
            choiceContext,
            player.Creature,
            DynamicVars["Footwork"].IntValue,
            player.Creature,
            null);
    }
}
