using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using flametail.Characters;
using flametail.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Cards;

/// <summary>
/// 已往不谏（旧版）：当一张诅咒牌从你的手牌进入弃牌堆时，改为将其消耗。
/// 已被自强不息（flametailPerseverance）重做替代（修改文档 [43]）。
/// 本卡保留类与效果以兼容旧存档读档（CardModel 按 ID 反序列化需要该类存在），
/// 但不再显示、不进卡池（showInCardLibrary=false）。
/// </summary>
[RegisterCard(typeof(flametailCardPool))]
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

    public flametailPastIsPast() : base(BaseEnergyCost, CardKind, CardRarityValue, CardTarget, ShowInCardLibrary)
    {
    }

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
