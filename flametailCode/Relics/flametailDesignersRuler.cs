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
/// 修改清单 [3] 裁定"最终加算"（乘算后加算）——由 DesignersRulerFinalAdditivePatch
/// 在引擎乘算循环全部结束后追加，而不是坐在乘算之前的标准 ModifyBlockAdditive 槽。
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

    /// <summary>
    /// 焰尾专属"乘算后加算"位：在 Hook.ModifyBlock 乘算循环全部结束后追加的最终加算
    /// （由 DesignersRulerFinalAdditivePatch 调用；标准 ModifyBlockAdditive 槽在乘算之前，
    /// 坐在那里会变成 (4+1)×0.75=3 而非要求的 4×0.75+1=4——修改清单 [3] 裁定"最终加算"）。
    /// 只对玩家自身生效；仅当乘算后仍为正格挡时才 +1，避免影响负格挡/减格挡修正。
    /// 修改清单 [2] 削弱：只有"卡牌以外"的格挡（能力、药水等，cardPlay 为 null）才 +1。
    /// cardSource == null 门槛：卡面预览（BlockVar.UpdateCardPreview）也传 cardPlay=null，
    /// 但带 cardSource=卡——不排除它会导致"卡面显示 +1、实际打出不加"的错位。
    /// </summary>
    public decimal FinalBlockAdditive(Creature target, decimal blockAfterMultiplicative, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner.Creature || blockAfterMultiplicative <= 0m || cardPlay != null || cardSource != null)
        {
            return 0m;
        }

        return DynamicVars["Block"].IntValue;
    }
}
