using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using flametail.Cards;
using flametail.Characters;
using flametail.Keywords;
using flametail.Nodes.Vfx;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace flametail.Visuals;

/// <summary>
/// 全局命中特效订阅者:焰尾使用<b>自己的非支援攻击牌</b>命中时,在每个受击目标身上
/// 随机播放一个 flipbook 命中特效(见 <see cref="FlipbookHitVfx"/>)。
///
/// 过滤条件:
/// - dealer 是焰尾(<see cref="flametailCharacter"/>);
/// - CardSource 非空(排除药水/遗物/怪物等非卡牌攻击);
/// - 牌属于焰尾卡池(排除其他角色的牌);
/// - 牌类型为攻击(排除支援/技能/能力);
/// - 不含 Support 关键词(双保险)。
///
/// 注意:必须用 <see cref="HookedSingletonModel"/>(HookType.Combat)全局订阅,
/// 不能挂在角色模型上——游戏的 <c>CombatState.IterateHookListeners</c> 只枚举
/// Powers/遗物/药水/球/卡牌等模型,<b>不包含 CharacterModel</b>,角色能力永远收不到钩子。
/// </summary>
[RegisterSingleton]
public sealed class FlametailHitVfxHook : HookedSingletonModel, IAttackHitHookListener
{
    private static int _triggerCount;

    public FlametailHitVfxHook() : base(HookType.Combat)
    {
    }

    public Task BeforeAttackHit(AttackHitContext context)
    {
        // 戍夜威光(召唤物斩击)卡专属命中特效:该卡含 Support 关键词,被下方通用
        // 随机池过滤排除,这里单独接官方合成版 nearl2_skill_03_hits。
        // 原生 1:1 渲染 + 加色材质(纯亮辉光内容,深色不透明像素=0,加色让淡色
        // 弧尾透过受击闪白不被吃掉)。
        if (context.Dealer?.Player?.Character is flametailCharacter
            && context.CardSource is flametailNocturnalBrightPower)
        {
            foreach (Creature? target in context.Targets)
            {
                if (target is { IsAlive: true })
                {
                    FlipbookHitVfx.Create(target, "nearl2_skill_03_hits");
                }
            }
            return Task.CompletedTask;
        }

        // 烛火一闪(召唤物斩击)卡专属命中特效:官方三段 hit 合成版
        // vvana_skill_03_hits。该卡不含 Support 关键词,本会落入通用随机池,
        // 已在 IsEligible 中排除,避免专属+随机双重特效。
        if (context.Dealer?.Player?.Character is flametailCharacter
            && context.CardSource is flametailCandleFlash)
        {
            foreach (Creature? target in context.Targets)
            {
                if (target is { IsAlive: true })
                {
                    FlipbookHitVfx.Create(target, "vvana_skill_03_hits");
                }
            }
            return Task.CompletedTask;
        }

        // 光箭支援(召唤物射箭)卡专属命中特效:fartth_skill_03_hit_01(HIT_POINT 挂点,
        // rotation_y 朝向攻击方向)。该卡含 Support 关键词,IsEligible 已排除,无双重特效。
        if (context.Dealer?.Player?.Character is flametailCharacter
            && context.CardSource is flametailFeatherSupport)
        {
            foreach (Creature? target in context.Targets)
            {
                if (target is { IsAlive: true })
                {
                    FlipbookHitVfx.Create(target, "fartth_skill_03_hit_01");
                }
            }
            return Task.CompletedTask;
        }

        // 铳炮支援(召唤物炮击)卡专属命中特效:ashlok_attack_01_hit_02(HIT_POINT 挂点)。
        // 该卡含 Support 关键词,IsEligible 已排除,无双重特效。
        if (context.Dealer?.Player?.Character is flametailCharacter
            && context.CardSource is flametailCannonSupport)
        {
            foreach (Creature? target in context.Targets)
            {
                if (target is { IsAlive: true })
                {
                    FlipbookHitVfx.Create(target, "ashlok_attack_01_hit_02");
                }
            }
            return Task.CompletedTask;
        }

        // 骑枪支援(召唤物突刺)卡专属命中特效:wildmn_skill_02_hit(HIT_POINT 挂点)。
        // 该卡含 Support 关键词,IsEligible 已排除,无双重特效。
        if (context.Dealer?.Player?.Character is flametailCharacter
            && context.CardSource is flametailLanceSupport)
        {
            foreach (Creature? target in context.Targets)
            {
                if (target is { IsAlive: true })
                {
                    FlipbookHitVfx.Create(target, "wildmn_skill_02_hit");
                }
            }
            return Task.CompletedTask;
        }

        if (!IsEligible(context))
        {
            return Task.CompletedTask;
        }

        if (Interlocked.Increment(ref _triggerCount) <= 3)
        {
            Entry.Logger.Info(
                $"[FlametailHitVfxHook] 命中钩子触发 #{_triggerCount}: dealer={context.Dealer?.Name}, " +
                $"card={context.CardSource?.GetType().Name}, targets={context.Targets.Count}");
        }

        foreach (Creature? target in context.Targets)
        {
            if (target is { IsAlive: true })
            {
                FlipbookHitVfx.Create(target);
            }
        }

        return Task.CompletedTask;
    }

    public Task AfterAttackHit(AttackHitContext context) => Task.CompletedTask;

    /// <summary>
    /// 攻击起手特效:焰尾的攻击动作(触发 attack 动画的 AttackCommand)开始时,
    /// 在焰尾脚底播放挥刀特效(attack01_start)。绑定"触发 attack 动画"这一行为,
    /// 与具体哪张攻击牌无关(药水/遗物/能力触发的攻击同样生效);
    /// 走 Cast/PowerUp 动画或不播动画的攻击不触发。
    /// </summary>
    public override Task BeforeAttack(AttackCommand command)
    {
        // _attackerAnimName 是 CreatureAnimator 的触发名("Attack"/"Cast"/"PowerUp"…),
        // 只有 "Attack" 会切到焰尾骨架的 attack 动画(见 CreatureAnimator.attackTrigger)。
        if (command.Attacker?.Player?.Character is flametailCharacter
            && s_shouldPlayAnimationField?.GetValue(command) is not false
            && s_attackerAnimNameField?.GetValue(command) as string == "Attack")
        {
            // 素材 FOOT_POINT 挂点:锚点像素 (211,381),图 553×543,中心 (276.5,271.5)
            // → offset = -(锚点-中心)×0.89 ≈ (+58,-98)。
            // 0.89 = 世界真实尺寸:骨架 setup 高 452.1 spine 单位 × 0.63(场景 SpineSprite
            // scale)= 284.8px 绘制高 = AK 1.25 世界单位 → 1 AK 单位 = 227.8px;227.8÷256 = 0.89。
            // 注意不能用 Bounds 悬停框(389px,带热区边距)当身高。挥刀 14 帧 @30fps ≈ 0.47s,
            // 与 attack 动画全长 0.53s 基本对拍。
            FootFlashVfx.Create(command.Attacker, "attack01_start", new Vector2(58.3f, -97.5f), 0.89f);
        }

        return Task.CompletedTask;
    }

    // AttackCommand 未公开"是否播动画"的只读属性,用反射读私有字段。
    private static readonly System.Reflection.FieldInfo? s_shouldPlayAnimationField =
        AccessTools.Field(typeof(AttackCommand), "_shouldPlayAnimation");

    private static readonly System.Reflection.FieldInfo? s_attackerAnimNameField =
        AccessTools.Field(typeof(AttackCommand), "_attackerAnimName");

    private static bool IsEligible(AttackHitContext context)
    {
        var card = context.CardSource;
        return context.Dealer?.Player?.Character is flametailCharacter
            && card?.Pool is flametailCardPool
            && card.Type == CardType.Attack
            && !card.CanonicalKeywords.Contains(FlametailKeywords.Support)
            // 烛火一闪走上方专属命中特效(vvana_skill_03_hits),不进随机池。
            && card is not flametailCandleFlash;
    }
}
