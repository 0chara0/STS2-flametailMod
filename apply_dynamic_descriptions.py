import json
import re
from pathlib import Path

card_dir = Path('flametailCode/Cards')
power_dir = Path('flametailCode/Powers')
loc_dir = Path('flametail/localization')

# ---------------------------------------------------------------------------
# Per-card configuration.
# Each entry:
#   vars: list of (name, base_value, upgrade_by)
#   custom: list of (old_regex_or_str, new_str, is_regex) replacements applied
#           after generic replacements.  is_regex defaults True.
# ---------------------------------------------------------------------------
CONFIG: dict[str, dict] = {
    'flametailStepGambit': {
        'vars': [('DrawAmount', 1, None)],
    },
    'flametailBlisteringCounter': {
        'vars': [('HitCount', 2, 1)],
        'custom': [
            (r'private const int BaseHitCount = 2;\n\s*', '', True),
            (r'int hitCount = BaseHitCount \+ \(IsUpgraded \? 1 : 0\);\n\s*', '', True),
            (r'\.WithHitCount\(hitCount\)', '.WithHitCount(DynamicVars["HitCount"].IntValue)'),
        ],
    },
    'flametailSideStep': {
        'vars': [('Footwork', 1, None)],
    },
    'flametailStrideSlash': {
        'vars': [('FootworkRemoved', 1, None)],
    },
    'flametailSweeping': {
        'vars': [('FootworkRemoved', 1, None)],
    },
    'flametailWhirlingAttack': {
        'vars': [('Footwork', 1, None), ('HitCount', 2, None)],
    },
    'flametailBackstep': {
        'vars': [('Footwork', 1, None)],
    },
    'flametailTurningAttack': {
        'vars': [('DrawAmount', 3, None)],
    },
    'flametailDeftAssault': {
        'vars': [('CounterHitCount', 3, None)],
    },
    'flametailFlourish': {
        'vars': [('HitCount', 3, None), ('CounterBonusHits', 2, None)],
        'custom': [
            (r'\.WithHitCount\(3 \+ _bonusCounterHits\)', '.WithHitCount(DynamicVars["HitCount"].IntValue + _bonusCounterHits)'),
            (r'_bonusCounterHits \+= 2;', '_bonusCounterHits += DynamicVars["CounterBonusHits"].IntValue;'),
        ],
    },
    'flametailDetour': {
        'vars': [('Footwork', 2, None)],
    },
    'flametailPinusSylvestris': {
        'vars': [('Footwork', 4, None), ('HitCount', 4, None)],
    },
    'flametailHiddenWeapon': {
        'vars': [('WeakAmount', 1, None), ('VulnerableAmount', 1, None)],
    },
    'flametailQuickIntuition': {
        'vars': [('Dodge', 1, None)],
    },
    'flametailPreFightIntel': {
        'vars': [('WeakAmount', 1, 1), ('VulnerableAmount', 1, 1)],
    },
    'flametailSingleFile': {
        'vars': [('VulnerableAmount', 2, None), ('WeakAmount', 2, None)],
    },
    'flametailRegulatedBreath': {
        'vars': [('DodgeRemoved', 1, None)],
    },
    'flametailProficiency': {
        'vars': [('DrawAmount', 1, None)],
    },
    'flametailVanguardSword': {
        'vars': [('DrawAmount', 1, None)],
    },
    'flametailReflexMove': {
        'vars': [('PlayAmount', 1, None)],
    },
    'flametailRefocus': {
        'vars': [('StrengthAmount', 1, None)],
    },
    'flametailGaleDash': {
        'vars': [('TargetFootwork', 4, None)],
        'custom': [
            (r'if \(currentFootwork < 4\)', 'if (currentFootwork < DynamicVars["TargetFootwork"].IntValue)'),
            (r'4 - currentFootwork', 'DynamicVars["TargetFootwork"].IntValue - currentFootwork'),
        ],
    },
    'flametailPhysicalAllocation': {
        'vars': [('FootworkPerEnergy', 2, None)],
        'custom': [
            (r'int energyGain = footwork\.Amount / 2;', 'int energyGain = footwork.Amount / DynamicVars["FootworkPerEnergy"].IntValue;'),
        ],
    },
    'flametailSteadyBlock': {
        'vars': [('MaxFootwork', 5, None)],
        'custom': [
            (r'int missing = int\.Max\(0, 5 - footwork\);', 'int missing = int.Max(0, DynamicVars["MaxFootwork"].IntValue - footwork);'),
        ],
    },
    'flametailWindUp': {
        'vars': [('RetainAmount', 1, None)],
    },
    'flametailRegainPosture': {
        'vars': [('DrawAmount', 2, None)],
    },
}

# Power files that need to use their Amount instead of a hardcoded 1.
POWER_REPLACEMENTS = {
    'flametailStepGambitPower.cs': [
        (r'await CardPileCmd\.Draw\(choiceContext, 1, Owner\.Player\);',
         'await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);'),
    ],
    'flametailRefocusPower.cs': [
        (r'PowerCmd\.Apply<StrengthPower>\(\s*null!,\s*Owner,\s*1,\s*Owner,\s*null\)',
         'PowerCmd.Apply<StrengthPower>(\n            null!,\n            Owner,\n            Amount,\n            Owner,\n            null)'),
    ],
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def add_using(text: str, ns: str) -> str:
    if ns in text:
        return text
    # insert after the last using statement
    last_using = None
    for m in re.finditer(r'^using .*?;', text, re.MULTILINE):
        last_using = m
    if last_using is None:
        return f"using {ns};\n{text}"
    end = last_using.end()
    return text[:end] + f"\nusing {ns};" + text[end:]


def add_canonical_vars(text: str, var_strings: list[str]) -> str:
    if not var_strings:
        return text
    # ensure DynamicVars using
    text = add_using(text, 'MegaCrit.Sts2.Core.Localization.DynamicVars')
    # ensure System.Collections.Generic for IEnumerable (most files already have it)
    text = add_using(text, 'System.Collections.Generic')

    pattern = re.compile(
        r'protected override IEnumerable<DynamicVar> CanonicalVars =>\s*\[\s*(.*?)\s*\];',
        re.DOTALL,
    )
    m = pattern.search(text)
    if m:
        existing = m.group(1).strip()
        if existing:
            # remove trailing comma if any
            existing = existing.rstrip().rstrip(',')
            new_content = existing + ',\n        ' + ',\n        '.join(var_strings)
        else:
            new_content = ',\n        '.join(var_strings)
        new_block = 'protected override IEnumerable<DynamicVar> CanonicalVars =>\n    [\n        ' + new_content + '\n    ];'
        text = text[:m.start()] + new_block + text[m.end():]
        return text

    # No CanonicalVars property; insert after AssetProfile line.
    asset_match = re.search(r'public override CardAssetProfile AssetProfile => new\(\s*\n\s*PortraitPath: [^\n]+\);', text)
    if not asset_match:
        raise ValueError('Could not find insertion point for CanonicalVars')
    insert_pos = asset_match.end()
    new_block = '\n\n    protected override IEnumerable<DynamicVar> CanonicalVars =>\n    [\n        ' + ',\n        '.join(var_strings) + '\n    ];'
    return text[:insert_pos] + new_block + text[insert_pos:]


def apply_generic_replacements(text: str, vars_cfg: list[tuple[str, int, int | None]]) -> str:
    var_names = {name for name, _, _ in vars_cfg}

    # Footwork gains
    if 'Footwork' in var_names:
        text = re.sub(
            r'(PowerCmd\.Apply<flametailFootworkPower>\(\s*choiceContext\s*,\s*[^,]+\s*,\s*)\d+(\s*,)',
            r'\1DynamicVars["Footwork"].IntValue\2',
            text,
        )
    # Dodge gains
    if 'Dodge' in var_names:
        text = re.sub(
            r'(PowerCmd\.Apply<flametailDodgePower>\(\s*choiceContext\s*,\s*[^,]+\s*,\s*)\d+(\s*,)',
            r'\1DynamicVars["Dodge"].IntValue\2',
            text,
        )
    # Weak
    if 'WeakAmount' in var_names:
        text = re.sub(
            r'(PowerCmd\.Apply<WeakPower>\(\s*choiceContext\s*,\s*[^,]+\s*,\s*)\d+(\s*,)',
            r'\1DynamicVars["WeakAmount"].IntValue\2',
            text,
        )
    # Vulnerable
    if 'VulnerableAmount' in var_names:
        text = re.sub(
            r'(PowerCmd\.Apply<VulnerablePower>\(\s*choiceContext\s*,\s*[^,]+\s*,\s*)\d+(\s*,)',
            r'\1DynamicVars["VulnerableAmount"].IntValue\2',
            text,
        )
    # Hit count (matches .WithHitCount(<digits>) and .WithHitCount(<digits> + _bonusCounterHits) handled custom)
    if 'HitCount' in var_names and 'CounterHitCount' not in var_names:
        text = re.sub(
            r'\.WithHitCount\(\s*\d+\s*\)',
            '.WithHitCount(DynamicVars["HitCount"].IntValue)',
            text,
        )
    if 'CounterHitCount' in var_names:
        text = re.sub(
            r'\.WithHitCount\(\s*\d+\s*\)',
            '.WithHitCount(DynamicVars["CounterHitCount"].IntValue)',
            text,
        )
    # Draw
    if 'DrawAmount' in var_names:
        text = re.sub(
            r'(CardPileCmd\.Draw\(\s*choiceContext\s*,\s*)\d+(\s*,\s*Owner)',
            r'\1DynamicVars["DrawAmount"].IntValue\2',
            text,
        )
        # In counter branch TurningAttack uses CardPileCmd.Draw(choiceContext, 3, Owner)
        text = re.sub(
            r'(CardPileCmd\.Draw\(\s*choiceContext\s*,\s*)\d+(\s*,\s*Owner)',
            r'\1DynamicVars["DrawAmount"].IntValue\2',
            text,
        )
    # Extra retain
    if 'RetainAmount' in var_names:
        text = re.sub(
            r'(PowerCmd\.Apply<flametailExtraCounterRetainPower>\(\s*choiceContext\s*,\s*[^,]+\s*,\s*)\d+(\s*,)',
            r'\1DynamicVars["RetainAmount"].IntValue\2',
            text,
        )
    # Generic power amount (for StepGambit / Proficiency / VanguardSword / ReflexMove / Refocus)
    # We replace the third numeric argument of PowerCmd.Apply for the power type used by the card.
    # Heuristic: only when target is Owner.Creature and the literal matches base value.
    for name, base, _ in vars_cfg:
        if name in ('DrawAmount', 'PlayAmount', 'StrengthAmount'):
            # Determine power type from the file text
            power_match = re.search(r'PowerCmd\.Apply<([A-Za-z0-9_]+)Power>\(', text)
            if power_match:
                power_type = power_match.group(1) + 'Power'
                text = re.sub(
                    rf'(PowerCmd\.Apply<{re.escape(power_type)}>\(\s*choiceContext\s*,\s*[^,]+\s*,\s*){base}(\s*,)',
                    rf'\1DynamicVars["{name}"].IntValue\2',
                    text,
                )
    # Dodge / Footwork removal
    if 'DodgeRemoved' in var_names:
        text = re.sub(
            r'(PowerCmd\.ModifyAmount\(\s*choiceContext\s*,\s*dodge\s*,\s*)-1(\s*,)',
            r'\1-DynamicVars["DodgeRemoved"].IntValue\2',
            text,
        )
    if 'FootworkRemoved' in var_names:
        text = re.sub(
            r'(PowerCmd\.ModifyAmount\(\s*choiceContext\s*,\s*footwork\s*,\s*)-1(\s*,)',
            r'\1-DynamicVars["FootworkRemoved"].IntValue\2',
            text,
        )

    return text


def add_on_upgrade(text: str, upgrades: list[tuple[str, int]]) -> str:
    if not upgrades:
        return text
    lines = []
    for name, delta in upgrades:
        lines.append(f'        DynamicVars["{name}"].UpgradeValueBy({delta});')
    upgrade_block = '\n'.join(lines)

    # Find OnUpgrade method
    m = re.search(r'protected override void OnUpgrade\(\)\s*\{', text)
    if m:
        # insert before the closing brace of OnUpgrade
        # locate the matching closing brace
        start = m.end()
        brace_count = 1
        i = start
        while i < len(text) and brace_count > 0:
            if text[i] == '{':
                brace_count += 1
            elif text[i] == '}':
                brace_count -= 1
            i += 1
        close_pos = i - 1
        # insert before close, with proper newline
        # find newline before close for indentation
        return text[:close_pos] + '\n' + upgrade_block + '\n' + text[close_pos:]
    else:
        # add new OnUpgrade method before final class closing brace
        insert_pos = text.rfind('}')
        new_method = '\n    protected override void OnUpgrade()\n    {\n' + upgrade_block + '\n    }\n'
        return text[:insert_pos] + new_method + text[insert_pos:]


# ---------------------------------------------------------------------------
# Apply C# changes
# ---------------------------------------------------------------------------

for class_name, cfg in CONFIG.items():
    file_path = card_dir / f'{class_name}.cs'
    if not file_path.exists():
        print(f'SKIP (missing): {file_path}')
        continue
    text = file_path.read_text(encoding='utf-8')
    original_text = text

    vars_cfg: list[tuple[str, int, int | None]] = cfg['vars']
    var_strings = [f'new IntVar("{name}", {base})' for name, base, _ in vars_cfg]

    text = add_canonical_vars(text, var_strings)
    text = apply_generic_replacements(text, vars_cfg)

    # Custom replacements
    for item in cfg.get('custom', []):
        old, new = item[0], item[1]
        is_regex = item[2] if len(item) > 2 else True
        if is_regex:
            text = re.sub(old, new, text)
        else:
            text = text.replace(old, new)

    # OnUpgrade additions
    upgrades = [(name, delta) for name, _, delta in vars_cfg if delta is not None]
    text = add_on_upgrade(text, upgrades)

    if text != original_text:
        file_path.write_text(text, encoding='utf-8')
        print(f'Updated {file_path.name}')
    else:
        print(f'No changes for {file_path.name}')

# Power files
for power_file, replacements in POWER_REPLACEMENTS.items():
    file_path = power_dir / power_file
    if not file_path.exists():
        continue
    text = file_path.read_text(encoding='utf-8')
    original_text = text
    for old, new, is_regex in [(o, n, True) for o, n in replacements]:
        text = re.sub(old, new, text)
    if text != original_text:
        file_path.write_text(text, encoding='utf-8')
        print(f'Updated {power_file}')

# ---------------------------------------------------------------------------
# Localization updates
# ---------------------------------------------------------------------------

# Mapping of card ID suffix (e.g. "FLAMETAIL_SIDE_STEP") to new description strings.
# For both English and Chinese.
LOC_REPLACEMENTS = {
    'FLAMETAIL_STEP_GAMBIT': {
        'eng': ("Whenever you gain Footwork, draw {DrawAmount} card(s).", "Whenever you gain Footwork, draw {DrawAmount} card(s)."),
        'zhs': ("每当你获得步法时，抽 {DrawAmount} 张牌。", "每当你获得步法时，抽 {DrawAmount} 张牌。"),
    },
    'FLAMETAIL_BLISTERING_COUNTER': {
        'eng': ("Retain. Deal {Damage:diff()} damage {HitCount:diff()} times. Counter: play the next Counter card in your hand.",
                "Retain. Deal {Damage:diff()} damage {HitCount:diff()} times. Counter: play the next Counter card in your hand."),
        'zhs': ("保留。造成 {Damage:diff()} 点伤害 {HitCount:diff()} 次。反制：额外打出手牌中的下一张反制牌。",
                "保留。造成 {Damage:diff()} 点伤害 {HitCount:diff()} 次。反制：额外打出手牌中的下一张反制牌。"),
    },
    'FLAMETAIL_SIDE_STEP': {
        'eng': ("Deal {Damage:diff()} damage. Gain {Footwork} Footwork.", "Deal {Damage:diff()} damage. Gain {Footwork} Footwork."),
        'zhs': ("造成 {Damage:diff()} 点伤害。获得 {Footwork} 层步法。", "造成 {Damage:diff()} 点伤害。获得 {Footwork} 层步法。"),
    },
    'FLAMETAIL_STRIDE_SLASH': {
        'eng': ("Can only be played while you have Footwork. Lose {FootworkRemoved} Footwork. Deal {Damage:diff()} damage. Return this card to your hand.",
                "Can only be played while you have Footwork. Lose {FootworkRemoved} Footwork. Deal {Damage:diff()} damage. Return this card to your hand."),
        'zhs': ("有步法时才能打出。失去 {FootworkRemoved} 层步法。造成 {Damage:diff()} 点伤害。将此牌返回你的手牌。",
                "有步法时才能打出。失去 {FootworkRemoved} 层步法。造成 {Damage:diff()} 点伤害。将此牌返回你的手牌。"),
    },
    'FLAMETAIL_SWEEPING': {
        'eng': ("Lose {FootworkRemoved} Footwork. Deal {Damage:diff()} damage to ALL enemies.", "Lose {FootworkRemoved} Footwork. Deal {Damage:diff()} damage to ALL enemies."),
        'zhs': ("失去 {FootworkRemoved} 层步法。对所有敌人造成 {Damage:diff()} 点伤害。", "失去 {FootworkRemoved} 层步法。对所有敌人造成 {Damage:diff()} 点伤害。"),
    },
    'FLAMETAIL_WHIRLING_ATTACK': {
        'eng': ("Gain {Footwork} Footwork. Deal {Damage:diff()} damage to ALL enemies {HitCount} times.",
                "Gain {Footwork} Footwork. Deal {Damage:diff()} damage to ALL enemies {HitCount} times."),
        'zhs': ("获得 {Footwork} 层步法。对所有敌人造成 {Damage:diff()} 点伤害 {HitCount} 次。",
                "获得 {Footwork} 层步法。对所有敌人造成 {Damage:diff()} 点伤害 {HitCount} 次。"),
    },
    'FLAMETAIL_BACKSTEP': {
        'eng': ("Gain {Block:diff()} Block. Gain {Footwork} Footwork. Counter.", "Gain {Block:diff()} Block. Gain {Footwork} Footwork. Counter."),
        'zhs': ("获得 {Block:diff()} 点格挡。获得 {Footwork} 层步法。反制。", "获得 {Block:diff()} 点格挡。获得 {Footwork} 层步法。反制。"),
    },
    'FLAMETAIL_TURNING_ATTACK': {
        'eng': ("Deal {Damage:diff()} damage. Add a Clumsy to your draw pile. Counter: Draw {DrawAmount} cards.",
                "Deal {Damage:diff()} damage. Add a Clumsy to your draw pile. Counter: Draw {DrawAmount} cards."),
        'zhs': ("造成 {Damage:diff()} 点伤害。将一张笨拙加入你的抽牌堆。反制：抽 {DrawAmount} 张牌。",
                "造成 {Damage:diff()} 点伤害。将一张笨拙加入你的抽牌堆。反制：抽 {DrawAmount} 张牌。"),
    },
    'FLAMETAIL_DEFT_ASSAULT': {
        'eng': ("Deal {Damage:diff()} damage to a random enemy for each Footwork you have. Counter: Discard this card and deal {Damage:diff()} damage to a random enemy {CounterHitCount} times.",
                "Deal {Damage:diff()} damage to a random enemy for each Footwork you have. Counter: Discard this card and deal {Damage:diff()} damage to a random enemy {CounterHitCount} times."),
        'zhs': ("你每有 1 层步法，随机造成 {Damage:diff()} 点伤害 1 次。反制：改为丢弃此牌，随机造成 {Damage:diff()} 点伤害 {CounterHitCount} 次。",
                "你每有 1 层步法，随机造成 {Damage:diff()} 点伤害 1 次。反制：改为丢弃此牌，随机造成 {Damage:diff()} 点伤害 {CounterHitCount} 次。"),
    },
    'FLAMETAIL_FLOURISH': {
        'eng': ("Deal {Damage:diff()} damage to a random enemy {HitCount} times. Deal additional damage equal to your Footwork. Counter: This card's hit count is increased by {CounterBonusHits} for the rest of combat.",
                "Deal {Damage:diff()} damage to a random enemy {HitCount} times. Deal additional damage equal to your Footwork. Counter: This card's hit count is increased by {CounterBonusHits} for the rest of combat."),
        'zhs': ("随机造成 {Damage:diff()} 点伤害 {HitCount} 次。额外造成等同于你步法值的伤害。反制：打出后，本场战斗中此牌伤害次数 +{CounterBonusHits}。",
                "随机造成 {Damage:diff()} 点伤害 {HitCount} 次。额外造成等同于你步法值的伤害。反制：打出后，本场战斗中此牌伤害次数 +{CounterBonusHits}。"),
    },
    'FLAMETAIL_DETOUR': {
        'eng': ("Deal {Damage:diff()} damage. If the target does not intend to Attack, gain {Footwork} Footwork.",
                "Deal {Damage:diff()} damage. If the target does not intend to Attack, gain {Footwork} Footwork."),
        'zhs': ("造成 {Damage:diff()} 点伤害。如果目标敌人的意图不是攻击，获得 {Footwork} 层步法。",
                "造成 {Damage:diff()} 点伤害。如果目标敌人的意图不是攻击，获得 {Footwork} 层步法。"),
    },
    'FLAMETAIL_PINUS_SYLVESTRIS': {
        'eng': ("Deal {Damage:diff()} damage to ALL enemies {HitCount} times. ALL players gain {Footwork} Footwork.",
                "Deal {Damage:diff()} damage to ALL enemies {HitCount} times. ALL players gain {Footwork} Footwork."),
        'zhs': ("对所有敌人造成 {Damage:diff()} 点伤害 {HitCount} 次。所有玩家获得 {Footwork} 层步法。",
                "对所有敌人造成 {Damage:diff()} 点伤害 {HitCount} 次。所有玩家获得 {Footwork} 层步法。"),
    },
    'FLAMETAIL_HIDDEN_WEAPON': {
        'eng': ("Deal {Damage:diff()} damage. Apply {WeakAmount} Weak and {VulnerableAmount} Vulnerable. Add a Shame to your discard pile. Exhaust.",
                "Deal {Damage:diff()} damage. Apply {WeakAmount} Weak and {VulnerableAmount} Vulnerable. Add a Shame to your discard pile. Exhaust."),
        'zhs': ("造成 {Damage:diff()} 点伤害。给予 {WeakAmount} 层虚弱和 {VulnerableAmount} 层易伤。将一张羞耻加入你的弃牌堆。消耗。",
                "造成 {Damage:diff()} 点伤害。给予 {WeakAmount} 层虚弱和 {VulnerableAmount} 层易伤。将一张羞耻加入你的弃牌堆。消耗。"),
    },
    'FLAMETAIL_QUICK_INTUITION': {
        'eng': ("Gain {Dodge} Dodge. Exhaust.", "Gain {Dodge} Dodge. Exhaust."),
        'zhs': ("获得 {Dodge} 层闪避。消耗。", "获得 {Dodge} 层闪避。消耗。"),
    },
    'FLAMETAIL_PRE_FIGHT_INTEL': {
        'eng': ("Innate. Apply {WeakAmount:diff()} Weak and {VulnerableAmount:diff()} Vulnerable to ALL enemies. Exhaust.",
                "Innate. Apply {WeakAmount:diff()} Weak and {VulnerableAmount:diff()} Vulnerable to ALL enemies. Exhaust."),
        'zhs': ("固有。给予所有敌人 {WeakAmount:diff()} 层虚弱和 {VulnerableAmount:diff()} 层易伤。消耗。",
                "固有。给予所有敌人 {WeakAmount:diff()} 层虚弱和 {VulnerableAmount:diff()} 层易伤。消耗。"),
    },
    'FLAMETAIL_SINGLE_FILE': {
        'eng': ("Apply {VulnerableAmount} Vulnerable to the target enemy. Apply {WeakAmount} Weak to all other enemies.",
                "Apply {VulnerableAmount} Vulnerable to the target enemy. Apply {WeakAmount} Weak to all other enemies."),
        'zhs': ("给予 {VulnerableAmount} 层易伤。给予其他敌人 {WeakAmount} 层虚弱。",
                "给予 {VulnerableAmount} 层易伤。给予其他敌人 {WeakAmount} 层虚弱。"),
    },
    'FLAMETAIL_REGULATED_BREATH': {
        'eng': ("If you have Dodge, remove {DodgeRemoved} Dodge and gain {Energy:diff()} [E].",
                "If you have Dodge, remove {DodgeRemoved} Dodge and gain {Energy:diff()} [E]."),
        'zhs': ("如果你有闪避，失去 {DodgeRemoved} 层闪避，获得 {Energy:diff()} 点[E]。",
                "如果你有闪避，失去 {DodgeRemoved} 层闪避，获得 {Energy:diff()} 点[E]。"),
    },
    'FLAMETAIL_PROFICIENCY': {
        'eng': ("Innate. Whenever you play an Innate card, draw {DrawAmount} card(s).",
                "Innate. Whenever you play an Innate card, draw {DrawAmount} card(s)."),
        'zhs': ("固有。每当你打出一张固有牌时，抽 {DrawAmount} 张牌。",
                "固有。每当你打出一张固有牌时，抽 {DrawAmount} 张牌。"),
    },
    'FLAMETAIL_VANGUARD_SWORD': {
        'eng': ("Whenever you Counter, draw {DrawAmount} card(s).", "Whenever you Counter, draw {DrawAmount} card(s)."),
        'zhs': ("每当你反制时，抽 {DrawAmount} 张牌。", "每当你反制时，抽 {DrawAmount} 张牌。"),
    },
    'FLAMETAIL_REFLEX_MOVE': {
        'eng': ("At the end of your turn, randomly play {PlayAmount} card(s) from your hand that grant(s) Footwork.",
                "At the end of your turn, randomly play {PlayAmount} card(s) from your hand that grant(s) Footwork."),
        'zhs': ("在你的回合结束时，随机打出手牌中的 {PlayAmount} 张能获得步法的牌。",
                "在你的回合结束时，随机打出手牌中的 {PlayAmount} 张能获得步法的牌。"),
    },
    'FLAMETAIL_REFOCUS': {
        'eng': ("Innate. Whenever a Curse card enters your discard or exhaust pile, gain {StrengthAmount} Strength.",
                "Innate. Whenever a Curse card enters your discard or exhaust pile, gain {StrengthAmount} Strength."),
        'zhs': ("固有。当一张诅咒牌进入消耗牌堆或弃牌堆时，你获得 {StrengthAmount} 点力量。",
                "固有。当一张诅咒牌进入消耗牌堆或弃牌堆时，你获得 {StrengthAmount} 点力量。"),
    },
    'FLAMETAIL_GALE_DASH': {
        'eng': ("Set your Footwork to {TargetFootwork}.", "Set your Footwork to {TargetFootwork}."),
        'zhs': ("将你的步法补至 {TargetFootwork} 层。", "将你的步法补至 {TargetFootwork} 层。"),
    },
    'FLAMETAIL_PHYSICAL_ALLOCATION': {
        'eng': ("Gain [E] for every {FootworkPerEnergy} Footwork you have. Lose all Footwork.",
                "Gain [E] for every {FootworkPerEnergy} Footwork you have. Lose all Footwork."),
        'zhs': ("你每有 {FootworkPerEnergy} 层步法，获得 [E]。失去所有步法。",
                "你每有 {FootworkPerEnergy} 层步法，获得 [E]。失去所有步法。"),
    },
    'FLAMETAIL_STEADY_BLOCK': {
        'eng': ("Gain {BlockPerFootwork:diff()} Block for each Footwork you are missing from {MaxFootwork}.",
                "Gain {BlockPerFootwork:diff()} Block for each Footwork you are missing from {MaxFootwork}."),
        'zhs': ("你的步法和 {MaxFootwork} 层每相差 1 层，获得 {BlockPerFootwork:diff()} 点格挡。",
                "你的步法和 {MaxFootwork} 层每相差 1 层，获得 {BlockPerFootwork:diff()} 点格挡。"),
    },
    'FLAMETAIL_WIND_UP': {
        'eng': ("Draw {DrawAmount:diff()} cards. Discard the non-Counter cards drawn. This turn, you may retain {RetainAmount} additional Counter card(s).",
                "Draw {DrawAmount:diff()} cards. Discard the non-Counter cards drawn. This turn, you may retain {RetainAmount} additional Counter card(s)."),
        'zhs': ("抽 {DrawAmount:diff()} 张牌，丢弃其中的非反制牌。本回合你可以多保留 {RetainAmount} 张反制牌。",
                "抽 {DrawAmount:diff()} 张牌，丢弃其中的非反制牌。本回合你可以多保留 {RetainAmount} 张反制牌。"),
    },
    'FLAMETAIL_REGAIN_POSTURE': {
        'eng': ("If you have no Footwork, gain {Footwork} Footwork and draw {DrawAmount} cards.",
                "If you have no Footwork, gain {Footwork} Footwork and draw {DrawAmount} cards."),
        'zhs': ("如果你没有步法，获得 {Footwork} 层步法，抽 {DrawAmount} 张牌。",
                "如果你没有步法，获得 {Footwork} 层步法，抽 {DrawAmount} 张牌。"),
    },
}


def update_loc(lang: str) -> None:
    path = loc_dir / lang / 'cards.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    for suffix, translations in LOC_REPLACEMENTS.items():
        desc, smart = translations[lang]
        data[f'FLAMETAIL_CARD_{suffix}.description'] = desc
        data[f'FLAMETAIL_CARD_{suffix}.smartDescription'] = smart
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Updated {path}')


update_loc('eng')
update_loc('zhs')

print('Done.')
