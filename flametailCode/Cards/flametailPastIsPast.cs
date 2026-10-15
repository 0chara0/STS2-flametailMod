using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using flametail.Keywords;
using MegaCrit.Sts2.Core.HoverTips;

namespace flametail.Cards;

/// <summary>
/// 已往不谏（旧版）：当一张诅咒牌从你的手牌进入弃牌堆时，改为将其消耗。
/// 已被自强不息（flametailPerseverance）重做替代（修改文档 [43]）。
/// <br/><b>已完全下架</b>（2026-10-15 用户裁定：不在意旧存档玩家，本类仅作代码备份）：
/// 移除 [RegisterCard]，ModelDb 中不存在本卡——卡牌奖励、商店、战斗生成、图鉴等一切
/// 途径均不可见。持有它的旧存档读档时经 SaveUtil.CardOrDeprecated 兜底为引擎
/// DeprecatedCard 占位卡。恢复方法：把 [RegisterCard(typeof(flametailCardPool))] 加回即可。
/// </summary>
public sealed class flametailPastIsPast : ModCardTemplate
{
    private const int BaseEnergyCost = 2;
    private const CardType CardKind = CardType.Power;
    private const CardRarity CardRarityValue = CardRarity.Rare;
    private const TargetType CardTarget = TargetType.Self;
    private const bool ShowInCardLibrary = false;

    private static readonly CardAssetProfile _assetProfile = new(
        PortraitPath: $"{Entry.ResPath}/images/cards/{"flametailPastIsPast"}.png");
    public override CardAssetProfile AssetProfile => _assetProfile;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[] {
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    };

    public flametailPastIsPast() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

    // 不可获取（见类注释）：挡住跨池修改器（Cursed Run 等）的生成路径。
    public override bool CanBeGeneratedInCombat => false;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<flametailPastIsPastPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
