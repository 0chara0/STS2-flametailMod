using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace flametail.Characters;

/// <summary>
/// 不对外可见的卡池，仅用于内部选择界面（如盔甲改良的选项）。
/// </summary>
[RegisterSharedCardPool]
public sealed class flametailHiddenCardPool : TypeListCardPoolModel
{
    private static readonly Material? PoolFrameTintMaterial =
        MaterialUtils.CreateRgbShaderMaterial(0.42f, 0.65f, 0.72f);

    public override string Title => "flametail_hidden";
    public override string EnergyColorName => "flametail";
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
    public override Color DeckEntryCardColor => flametailCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.08f, 0.18f, 0.24f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;
    public override bool IsColorless => true;
}
