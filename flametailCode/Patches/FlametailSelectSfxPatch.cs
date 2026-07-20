using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using STS2RitsuLib.Audio;

namespace flametail.Patches;

/// <summary>
/// 拦截焰尾的角色选择音效 sentinel，改播 mod 里自带的 WAV 样本。
/// 使用 RitsuLib 的 GameAudioService 而不是手动创建 Godot 播放器。
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
public static class FlametailSelectSfxPatch
{
    private const string FlametailSelectSfx = "event:/sfx/characters/flametail/flametail_select";
    private const string FlametailSelectSamplePath = "res://flametail/audio/characters/flametail_select.wav";

    public static bool Prefix(string sfx, float volume)
    {
        if (sfx != FlametailSelectSfx)
        {
            return true;
        }

        var source = AudioSource.ResourceFile(FlametailSelectSamplePath);
        var options = new AudioPlaybackOptions
        {
            Volume = volume,
            Routing = new AudioRoutingOptions { Channel = "Sfx" },
        };

        GameAudioService.Shared.PlayOneShot(source, options);
        return false;
    }
}
