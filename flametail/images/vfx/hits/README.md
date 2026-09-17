# 命中特效素材目录(Hit VFX Sources)

每个命中特效一个子文件夹,把素材复制到子文件夹里即可。命名用 snake_case,例如:

```
flametail/images/vfx/hits/
    flame_slash/        ← 每个特效一个文件夹
    ice_shatter/
    holy_impact/
```

## 文件格式(按优先级)

1. **帧序列 PNG**(最推荐):带 alpha 通道,命名 `frame_0001.png`、`frame_0002.png` … 连续编号
2. **动画 WebP / GIF 源文件**:直接放进子文件夹,由脚本抽帧(会自动生成 `frames/` 子目录)
3. **分层素材**(可选):如果发光层和实体层能分开,用前缀区分:
   - `glow_frame_0001.png` — 发光层(半透明光晕,运行时用 additive 混合)
   - `body_frame_0001.png` — 实体层(烟、碎片,普通混合)

## 子文件夹里的可选说明文件

- `meta.txt`:每行一个 `键=值`,目前支持:
  - `fps=24` — 目标帧率(不写默认 24)
  - `loop=true|false` — 是否循环(命中特效一般 false)
  - `scale=1.0` — 导入时的缩放参考

## 质量要求

- **分辨率**:不低于游戏内显示尺寸(拿不准就 ≥512px 见方)
- **半透明光晕**:必须是带 alpha 渐变的 PNG/WebP(8-bit alpha),不要黑底、不要 GIF 的硬边透明
- **帧数**:命中特效一般 10~40 帧(0.4~1.5 秒 @24fps),首帧干净、末帧完全消散

## 复制好之后

把特效文件夹名告诉 Claude,后续流程全自动:

1. 抽帧 + 质检(分辨率 / 帧数 / alpha)
2. 生成 `SpriteFrames` 资源(单图集)
3. 在 `flametail/scenes/vfx/hits/<特效名>.tscn` 建 FlipbookVfx 场景
4. 接到卡牌 / Power 的事件点上
