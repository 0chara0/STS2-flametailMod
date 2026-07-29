using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;

namespace flametail.Powers;

/// <summary>
/// 再快一点：步法不会低于 2 层。该能力不可叠加。
/// </summary>
[RegisterPower]
public sealed class flametailEvenFasterPower : ModPowerTemplate
{
    public const int MinimumFootwork = 2;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static readonly PowerAssetProfile _assetProfile = new(
        IconPath: $"{Entry.ResPath}/images/powers/flametailEvenFasterPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/flametailEvenFasterPower.png");
    public override PowerAssetProfile AssetProfile => _assetProfile;
}
