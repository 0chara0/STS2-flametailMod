using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace flametail.Patches;

/// <summary>
/// 战斗开始时后台预载焰尾全部 VFX 资源,消除特效首次播放的卡顿。
///
/// 根因:游戏的 <see cref="AssetCache"/> 对未预载资源走同步 <c>ResourceLoader.Load</c>
/// (SpriteFrames → 解压最大 400MB+ 的图集 PNG → 上传 GPU),而进任何房间时
/// <c>PreloadManager.LoadRoomAssets</c> 都会 <c>UnloadMissedCacheAssets()</c> 把这些
/// 迟加载资源卸掉——于是每场战斗特效第一次出现都要在攻击/命中瞬间同步加载,明显掉帧。
///
/// 做法:不走卡牌映射(临时生成的卡牌无从枚举,维护映射表也繁琐),直接在
/// <see cref="Hook.BeforeCombatStart"/>(此时房间资源集已切换完毕,战斗期间不会再有
/// LoadAssetSets 卸载)枚举 generated 目录下所有特效的 SpriteFrames、全部命中/召唤
/// 场景与材质,交给游戏自带的后台预载会话(<see cref="AssetCache.CreateSession"/> +
/// <c>NAssetLoader.LoadInTheBackground</c>,线程化分批加载、VFX 场景单载避竞态、
/// 加载结果进正式缓存不占 missed 名额)。目录枚举 = 新增特效零维护自动纳入。
/// 全量约 2.1GB 解压显存,后台渐进加载,出房时游戏按正常生命周期回收。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class VfxPreloadPatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState)
    {
        if (TestMode.IsOn)
        {
            return;
        }

        var paths = new HashSet<string>
        {
            // 两个命中特效材质(SummonedAllyVfx/FootFlashVfx/FlipbookHitVfx 共用)。
            $"{Entry.ResPath}/scenes/vfx/hits/hit_vfx_add.tres",
            $"{Entry.ResPath}/scenes/vfx/hits/hit_vfx_premul.tres",
        };

        // generated/<特效id>/<特效id>_frames.tres:逐目录自动发现,与 build_hit_vfx.py 输出对齐。
        if (DirAccess.Open($"{Entry.ResPath}/images/vfx/hits/generated") is { } genDir)
        {
            foreach (string id in genDir.GetDirectories())
            {
                string frames = $"{Entry.ResPath}/images/vfx/hits/generated/{id}/{id}_frames.tres";
                if (ResourceLoader.Exists(frames))
                {
                    paths.Add(frames);
                }
            }
        }

        // 命中特效播放场景(hits/*.tscn,FlipbookHitVfx 用)与召唤物场景(summons/*.tscn)。
        CollectScenes(paths, $"{Entry.ResPath}/scenes/vfx/hits");
        CollectScenes(paths, $"{Entry.ResPath}/scenes/vfx/summons");

        // 后台会话由 NAssetLoader 逐帧泵(ResourceLoader 线程化加载,批 128),
        // 完成前特效仍走同步加载兜底,故 fire-and-forget 即可。
        AssetLoadingSession session = PreloadManager.Cache.CreateSession("flametail-vfx", paths);
        _ = NAssetLoader.Instance.LoadInTheBackground(session);
    }

    private static void CollectScenes(HashSet<string> paths, string dirPath)
    {
        if (DirAccess.Open(dirPath) is not { } dir)
        {
            return;
        }
        foreach (string file in dir.GetFiles().Where(f => f.EndsWith(".tscn")))
        {
            paths.Add($"{dirPath}/{file}");
        }
    }
}
