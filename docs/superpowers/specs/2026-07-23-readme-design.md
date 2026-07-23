# README 设计文档

## 目标
为 `flametailMod` 项目编写一份中文 README，帮助开发者快速理解项目结构并知道如何扩展内容。

## 语言
中文

## README 结构

### 1. 项目结构
说明仓库主要目录与文件的作用：

- `flametailCode/` —— C# 源代码
  - `Cards/` —— 卡牌逻辑
  - `Powers/` —— 能力（Power）逻辑
  - `Relics/` —— 遗物逻辑
  - `Characters/` —— 角色、卡池、药水池、遗物池
  - `Entry.cs` —— Mod 入口，负责初始化、自动注册、关键词注册、Harmony 补丁
- `flametail/` —— 游戏资源与配置
  - `localization/` —— 中英文本地化 JSON
  - `scenes/` —— Godot 场景文件
  - `images/` —— 图片资源（卡牌、角色、遗物）
- `flametail.json` —— Mod 清单文件
- `flametail.csproj` —— MSBuild 项目文件，包含构建、PCK 导出、依赖同步逻辑
- `local.props.template` —— 本地游戏路径配置模板

### 2. 如何添加内容
分点说明扩展 Mod 内容的步骤：

- **新增卡牌**
  - 在 `flametailCode/Cards/` 下创建继承自合适基类的 C# 类
  - 使用 `[RegisterCard]` 等 RitsuLib attribute 自动注册
  - 在 `flametail/localization/zhs/` 与 `flametail/localization/eng/` 中添加对应本地化键
- **新增能力**
  - 在 `flametailCode/Powers/` 下创建类并继承 `Power`
  - 使用 `[RegisterPower]` 注册
  - 添加本地化文本
- **新增遗物**
  - 在 `flametailCode/Relics/` 下创建类
  - 使用 `[RegisterRelic]` 注册
- **新增角色相关内容**
  - 卡池、药水池、遗物池分别对应 `flametailCode/Characters/` 下的类
  - 角色主类使用 `[RegisterCharacter]` 注册
- **本地化键名规则**
  - 说明键名与代码中 `CanonicalKeywords`、`CardName`、`PowerName` 等字段的对应关系
  - 强调中英文文件需要同步维护

## 不包含的内容
- 项目简介/背景
- 安装与构建步骤
- 贡献规范
- 许可与声明

## 成功标准
- README 为中文
- 只包含“项目结构”和“如何添加内容”两个主要章节
- 内容准确反映当前仓库的目录与注册机制
