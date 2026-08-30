using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 设计师量尺：你以任何方式获得的格挡值增加 1 点。
/// 通过 ModifyBlockAdditive 对每次格挡获得量（技能/药水/遗物等所有来源）额外 +1。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailDesignersRuler : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Block", 1)
    ];

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailDesignersRuler)}.png",
        // 描边图：遗物图鉴按所属角色池染主题色，需用独立轮廓剪影图（_r4 为选定的描边版本）。
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailDesignersRuler)}_r4.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailDesignersRuler)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 只对玩家自身生效；仅当实际获得正格挡时才 +1，避免影响负格挡/减格挡修正。
        if (target != Owner.Creature || block <= 0m)
        {
            return 0m;
        }

        return DynamicVars["Block"].IntValue;
    }
}
