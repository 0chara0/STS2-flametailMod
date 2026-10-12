using Godot;
using FileAccess = Godot.FileAccess;
using DirAccess = Godot.DirAccess;

namespace flametail.Helpers;

/// <summary>
/// 开发工具：把原版 UI 纹理（卡框/肖像边框/能量图标等）dump 成 PNG，
/// 供设计稿对齐真实尺寸与布局。资源加载失败只记日志，绝不影响 mod 启动。
/// 输出目录：E:/aitool/flametailMod/tools/pck_dump/
/// </summary>
public static class DumpUiTextures
{
    private const string OutDir = "E:/aitool/flametailMod/tools/pck_dump";

    private static readonly string[] Paths =
    [
        "atlases/ui_atlas.sprites/card/card_frame_attack_s.tres",
        "atlases/ui_atlas.sprites/card/card_frame_skill_s.tres",
        "atlases/ui_atlas.sprites/card/card_frame_power_s.tres",
        "atlases/ui_atlas.sprites/card/card_portrait_border_attack_s.tres",
        "atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres",
        "atlases/ui_atlas.sprites/card/card_portrait_border_power_s.tres",
        "atlases/ui_atlas.sprites/card/energy_ironclad.tres",
        "atlases/ui_atlas.sprites/card/energy_colorless.tres",
        "atlases/ui_atlas.sprites/card/energy_flametail.tres",
        "atlases/compressed_atlas.sprites/ancient_card_border.png.tres",
        // 原版角色能量球 5 层——作为"好看"的质感标杆与分层参照
        "images/characters/ironclad/ironclad_energy_orb_layer_1.png",
        "images/characters/ironclad/ironclad_energy_orb_layer_2.png",
        "images/characters/ironclad/ironclad_energy_orb_layer_3.png",
        "images/characters/ironclad/ironclad_energy_orb_layer_4.png",
        "images/characters/ironclad/ironclad_energy_orb_layer_5.png",
        "images/characters/silent/silent_energy_orb_layer_1.png",
        "images/characters/silent/silent_energy_orb_layer_2.png",
        "images/characters/silent/silent_energy_orb_layer_3.png",
        "images/characters/silent/silent_energy_orb_layer_4.png",
        "images/characters/silent/silent_energy_orb_layer_5.png",
        "images/characters/defect/defect_energy_orb_layer_1.png",
        "images/characters/defect/defect_energy_orb_layer_2.png",
        "images/characters/defect/defect_energy_orb_layer_3.png",
        "images/characters/defect/defect_energy_orb_layer_4.png",
        "images/characters/defect/defect_energy_orb_layer_5.png",
    ];

    // 卡框 shader 材质：游戏内卡框观感 = Frame 纹理 × 该材质（NCard.cs:793），
    // 导出 shader 代码以弄清原版的染色/明暗到底怎么算的。
    private static readonly string[] MaterialPaths =
    [
        "materials/cards/frames/card_frame_red_mat.tres",
        "materials/cards/frames/card_frame_blue_mat.tres",
        "materials/cards/frames/card_frame_green_mat.tres",
        "materials/cards/frames/card_frame_orange_mat.tres",
        "materials/cards/frames/card_frame_pink_mat.tres",
        "materials/cards/frames/card_frame_colorless_mat.tres",
        "materials/cards/frames/card_frame_curse_mat.tres",
        "materials/cards/frames/card_frame_quest_mat.tres",
    ];

    public static void Run()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(OutDir);

            foreach (string inner in Paths)
            {
                foreach (string prefix in new[] { "res://images/", "res://" })
                {
                    string path = prefix + inner;
                    try
                    {
                        if (!ResourceLoader.Exists(path))
                        {
                            continue;
                        }

                        DumpTexture(path, inner.Replace('/', '_'));
                        break;
                    }
                    catch (System.Exception e)
                    {
                        Entry.Logger.Warn($"DumpUiTextures: {path} 失败: {e.Message}");
                    }
                }
            }

            foreach (string inner in MaterialPaths)
            {
                string path = "res://" + inner;
                try
                {
                    if (!ResourceLoader.Exists(path))
                    {
                        Entry.Logger.Warn($"DumpUiTextures: {path} 不存在");
                        continue;
                    }

                    DumpMaterial(path, inner.Replace('/', '_'));
                }
                catch (System.Exception e)
                {
                    Entry.Logger.Warn($"DumpUiTextures: {path} 失败: {e.Message}");
                }
            }

            Entry.Logger.Info("DumpUiTextures 完成");
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"DumpUiTextures 整体失败: {e.Message}");
        }
    }

    private static void DumpTexture(string path, string name)
    {
        if (ResourceLoader.Load(path) is not Texture2D tex)
        {
            Entry.Logger.Warn($"DumpUiTextures: {path} 不是 Texture2D");
            return;
        }

        // AtlasTexture：取源图对应区域；同时把源图整张也 dump 一份便于查布局。
        Image img;
        if (tex is AtlasTexture at)
        {
            Image src = at.Atlas.GetImage();
            if (src.IsCompressed())
            {
                src.Decompress();
            }

            string srcName = $"{name}__source_{src.GetWidth()}x{src.GetHeight()}.png";
            if (!FileAccess.FileExists($"{OutDir}/{srcName}"))
            {
                src.SavePng($"{OutDir}/{srcName}");
            }

            Rect2I region = (Rect2I)at.Region;
            img = src.GetRegion(region);
        }
        else
        {
            img = tex.GetImage();
            if (img.IsCompressed())
            {
                img.Decompress();
            }
        }

        string file = $"{name}_{img.GetWidth()}x{img.GetHeight()}.png";
        string outPath = $"{OutDir}/{file}";
        if (FileAccess.FileExists(outPath))
        {
            return; // 已导出过，跳过
        }

        img.SavePng(outPath);
        Entry.Logger.Info($"DumpUiTextures: {path} -> {file}");
    }

    private static void DumpMaterial(string path, string name)
    {
        if (ResourceLoader.Load(path) is not ShaderMaterial mat || mat.Shader == null)
        {
            Entry.Logger.Warn($"DumpUiTextures: {path} 不是 ShaderMaterial 或无 shader");
            return;
        }

        string outPath = $"{OutDir}/{name}.gd";
        if (FileAccess.FileExists(outPath))
        {
            return; // 已导出过，跳过
        }

        using FileAccess f = FileAccess.Open(outPath, FileAccess.ModeFlags.Write);
        f.StoreString(mat.Shader.Code);
        Entry.Logger.Info($"DumpUiTextures: {path} -> {name}.gd");
    }
}
