extends SceneTree

# 批量把 mod 里通过 .import 重映射到 .ctex 的纹理还原为 PNG 源文件。
# 用法：godot --headless --path flametail --script convert_ctex_to_png.gd

func _init():
    var root := "res://flametail"
    var imports := _find_imports(root)
    var converted := 0
    var failed := 0

    for import_path in imports:
        var cfg := ConfigFile.new()
        var err := cfg.load(import_path)
        if err != OK:
            push_warning("无法读取 %s，错误 %d" % [import_path, err])
            failed += 1
            continue

        if cfg.get_value("remap", "importer", "") != "texture":
            continue

        var source_file := cfg.get_value("deps", "source_file", "") as String
        if source_file.is_empty():
            # 有些手写的 .import 没有 [deps]，直接从 .import 路径推断源文件
            source_file = import_path.trim_suffix(".import")
        if source_file.is_empty():
            continue

        # 只处理 PNG 源文件
        if not source_file.ends_with(".png"):
            continue

        print("Converting: %s" % source_file)

        var tex := ResourceLoader.load(source_file, "CompressedTexture2D", ResourceLoader.CACHE_MODE_IGNORE)
        if tex == null:
            push_error("无法加载纹理: %s" % source_file)
            failed += 1
            continue

        var img: Image = tex.get_image()
        if img == null:
            push_error("无法获取 Image: %s" % source_file)
            failed += 1
            continue

        err = img.save_png(source_file)
        if err != OK:
            push_error("保存 PNG 失败 %s，错误 %d" % [source_file, err])
            failed += 1
            continue

        # 删除旧的 .import，让 Godot 下次打开时重新从 PNG 导入
        var f := FileAccess.open(import_path, FileAccess.READ)
        if f != null:
            f = null
            var da := DirAccess.open("res://")
            if da != null:
                err = da.remove(import_path.trim_prefix("res://"))
                if err != OK:
                    push_warning("无法删除旧 .import %s，错误 %d" % [import_path, err])

        converted += 1

    print("Done. converted=%d failed=%d" % [converted, failed])
    quit()

func _find_imports(path: String) -> Array[String]:
    var result: Array[String] = []
    var dir := DirAccess.open(path)
    if dir == null:
        push_error("无法打开目录 %s" % path)
        return result

    dir.list_dir_begin()
    var file_name := dir.get_next()
    while file_name != "":
        var full_path := path.path_join(file_name)
        if dir.current_is_dir():
            if not file_name.begins_with("."):
                result.append_array(_find_imports(full_path))
        elif file_name.ends_with(".import"):
            result.append(full_path)
        file_name = dir.get_next()
    dir.list_dir_end()
    return result
