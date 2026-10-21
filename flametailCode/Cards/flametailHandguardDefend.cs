using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using flametail.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 护手防御（修改文档 2026-10-21 用户裁定）：1 费，获得 6（8）点格挡。
/// 选择抽牌堆中的一张攻击（升级后：任意）牌放至抽牌堆顶部。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
public sealed class flametailHandguardDefend : ModCardTemplate
{
    private const int BaseEnergyCost = 1;
    private const CardType CardKind = CardType.Skill;
    private const CardRarity CardRarityValue = CardRarity.Common;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = true;

    public override bool GainsBlock => true;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailHandguardDefend"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6, ValueProp.Move)
    ];

    public flametailHandguardDefend() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var drawPile = Owner.PlayerCombatState?.DrawPile;
        if (drawPile == null || drawPile.Cards.Count == 0)
        {
            return;
        }

        // 基础只在攻击牌中选，升级后任选（修改文档 2026-10-21：基础也是选择，不再随机）。
        IEnumerable<CardModel> pool = drawPile.Cards;
        if (!IsUpgraded)
        {
            pool = pool.Where(card => card.Type == CardType.Attack).ToList();
        }

        List<CardModel> candidates = pool.ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        // 由玩家选择一张放至抽牌堆顶部（不可取消）。
        var prefs = new CardSelectorPrefs(
            new LocString("cards", "FLAMETAIL_HANDGUARD_DEFEND_PROMPT"),
            1,
            1)
        {
            Cancelable = false,
        };

        CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            prefs)).FirstOrDefault();

        if (chosen == null)
        {
            return;
        }

        await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
