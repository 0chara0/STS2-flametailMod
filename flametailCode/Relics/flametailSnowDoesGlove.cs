using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 雪牝的护手：战斗开始时，每有 1 名敌人，你获得 1 点力量和 1 层步法。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailSnowDoesGlove : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailSnowDoesGlove)}.png",
        // 描边图：遗物图鉴按所属角色池染主题色，需用独立轮廓剪影图（_r4 为选定的描边版本）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailSnowDoesGlove)}_r4.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailSnowDoesGlove)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task BeforeCombatStart()
    {
        ICombatState? combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        int n = combatState.Enemies.Count(e => e.IsAlive);
        if (n <= 0)
        {
            return;
        }

        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, Owner.Creature, n, Owner.Creature, null);
        await PowerCmd.Apply<flametailFootworkPower>(context, Owner.Creature, n, Owner.Creature, null);
    }
}
