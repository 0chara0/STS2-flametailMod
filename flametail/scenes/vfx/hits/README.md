# 命中特效场景目录(Hit VFX Scenes)

每个命中特效对应一个场景:`<特效名>.tscn`,与 `flametail/images/vfx/hits/<特效名>/` 的素材文件夹同名。

- 素材规范见 `flametail/images/vfx/hits/README.md`
- 场景由脚本生成:根节点为 FlipbookVfx(Node2D),子节点为 AnimatedSprite2D
  (发光层挂 additive shader,实体层普通混合)
- 使用方式:命中事件点(`AttackHitHook` / `WithHitVfxNode`)实例化,播完自动 QueueFree

复制素材前此目录可以为空;素材就位后由 Claude 生成场景。
