# 焰尾 Mod（flametailMod）

https://www.bilibili.com/opus/1223036506427359252

![Happy](README.assets/Happy-1784807443553.gif)

## 项目结构

```
flametailMod/
├── flametailCode/              # C# 源代码
│   ├── Cards/                  # 卡牌逻辑
│   ├── Powers/                 # 能力（Power）逻辑
│   ├── Relics/                 # 遗物逻辑
│   ├── Characters/             # 角色、卡池、药水池、遗物池
│   └── Entry.cs                # Mod 入口：初始化、自动注册、关键词注册、Harmony 补丁
├── flametail/                  # 游戏资源与配置
│   ├── localization/           # 中英文本地化 JSON
│   │   ├── eng/
│   │   └── zhs/
│   ├── scenes/                 # Godot 场景文件
│   └── images/                 # 图片资源（卡牌、角色、遗物）
├── flametail.json              # Mod 清单文件
├── flametail.csproj            # MSBuild 项目文件（构建、PCK 导出、依赖同步）
└── local.props.template        # 本地游戏路径配置模板
```

## 如何添加内容

### 新增卡牌

1. 在 `flametailCode/Cards/` 下创建新的 C# 类，继承合适的卡牌基类。
2. 为类添加 RitsuLib 提供的 attribute，例如 `[RegisterCard(typeof(flametailCardPool))]`，这样 `Entry.cs` 中的 `ModTypeDiscoveryHub.RegisterModAssembly(...)` 会自动完成注册。
3. 在 `flametail/localization/zhs/` 和 `flametail/localization/eng/` 中添加对应本地化键。
   - 卡牌名称键示例：`FLAMETAIL_CARD_<CARD_NAME>.name`
   - 卡牌描述键示例：`FLAMETAIL_CARD_<CARD_NAME>.description`

### 新增能力

1. 在 `flametailCode/Powers/` 下创建新的 C# 类，继承 `Power` 或其子类。
2. 使用 `[RegisterPower]` attribute 注册。
3. 在 `flametail/localization/` 中添加能力名称与描述。

### 新增遗物

1. 在 `flametailCode/Relics/` 下创建新的 C# 类，继承合适的遗物基类。
2. 使用 `[RegisterRelic(typeof(flametailRelicPool))]` attribute 注册。
3. 在 `flametail/localization/` 中添加遗物名称与描述。

### 新增角色相关内容

- 卡池：`flametailCode/Characters/flametailCardPool.cs`
- 药水池：`flametailCode/Characters/flametailPotionPool.cs`
- 遗物池：`flametailCode/Characters/flametailRelicPool.cs`
- 角色主类：`flametailCode/Characters/flametailCharacter.cs`，使用 `[RegisterCharacter]` attribute 注册。

### 本地化键名规则

- 代码中通过 `CanonicalKeywords`、`CardName`、`PowerName` 等字段与 JSON 中的键名对应。
- 键名通常以 `FLAMETAIL_` 为前缀，后接类型（`CARD_`、`POWER_`、`RELIC_`、`CHARACTER_`）和具体标识符。
- 修改或新增键后，请同步维护 `eng/` 和 `zhs/` 两份文件，保持中英文一致。
