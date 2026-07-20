using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;

namespace flametail.Patches;

/// <summary>
/// 拦截焰尾的角色选择音效 sentinel，改播 mod 里自带的 YanWei_Select.sample。
/// 因为原项目用 FMOD event 路径，而 mod 只有一个 WAV 样本，所以用 Harmony 在 SfxCmd.Play 层做重定向。
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
public static class FlametailSelectSfxPatch
{
    private const string FlametailSelectSfx = "event:/sfx/characters/flametail/flametail_select";
    private const string FlametailSelectSamplePath = "res://flametail/audio/characters/flametail_select.wav";

    private static AudioStreamWav? _cachedStream;

    public static bool Prefix(string sfx, float volume)
    {
        if (sfx != FlametailSelectSfx)
        {
            return true;
        }

        PlaySample(volume);
        return false;
    }

    private static void PlaySample(float volume)
    {
        var stream = _cachedStream;
        if (stream == null || !GodotObject.IsInstanceValid(stream))
        {
            stream = ResourceLoader.Load<AudioStreamWav>(FlametailSelectSamplePath);
            if (stream == null || !GodotObject.IsInstanceValid(stream))
            {
                GD.PushWarning($"FlametailSelectSfxPatch: failed to load {FlametailSelectSamplePath}");
                return;
            }

            _cachedStream = stream;
        }

        var player = new AudioStreamPlayer
        {
            Stream = stream,
            VolumeDb = Mathf.LinearToDb(volume),
        };

        if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
        {
            tree.Root.AddChild(player);
            player.Play();
            player.Finished += () => player.QueueFree();
        }
        else
        {
            player.QueueFree();
        }
    }
}
