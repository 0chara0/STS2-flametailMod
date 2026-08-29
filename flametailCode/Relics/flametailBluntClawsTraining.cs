using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Relics;

/// <summary>
/// 钝爪-典训：获得时，选择升级你卡组中的一张牌，并为其附魔：sown（游戏自带附魔）。
/// 选择界面走卡组附魔预览（FromDeckForEnchantment），选定后升级 + 附魔。
/// </summary>
[RegisterRelic(typeof(flametailRelicPool))]
public sealed class flametailBluntClawsTraining : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    // 获得时一次性生效，不应再被交换掉（与 DollysMirror 等同款）。
    public override bool HasUponPickupEffect => true;

    private static readonly RelicAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailBluntClawsTraining)}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{nameof(flametailBluntClawsTraining)}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{nameof(flametailBluntClawsTraining)}.png");
    public override RelicAssetProfile AssetProfile => _assetProfile;

    public override async Task AfterObtained()
    {
        var prefs = new CardSelectorPrefs(
            new LocString("relics", "FLAMETAIL_RELIC_BLUNT_CLAWS_TRAINING_PICK_PROMPT"),
            1);

        // 从卡组中选择一张可被 sown 附魔的牌（附魔选择界面自带预览）。
        CardModel? card = (await CardSelectCmd.FromDeckForEnchantment(Owner, ModelDb.Enchantment<Sown>(), 1, prefs)).FirstOrDefault();
        if (card == null)
        {
            return;
        }

        CardCmd.Upgrade(card);
        CardCmd.Enchant<Sown>(card, 1);
    }
}
