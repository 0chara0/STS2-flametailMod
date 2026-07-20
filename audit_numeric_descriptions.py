import re
from pathlib import Path

card_dir = Path('flametailCode/Cards')

# Patterns to find numeric literals in common effect calls.
patterns = [
    # Footwork gain
    (r'PowerCmd\.Apply<flametailFootworkPower>\([^,]*,[^,]*,\s*(\d+)\s*,', 'Footwork'),
    # Dodge gain
    (r'PowerCmd\.Apply<flametailDodgePower>\([^,]*,[^,]*,\s*(\d+)\s*,', 'Dodge'),
    # Weak
    (r'PowerCmd\.Apply<WeakPower>\([^,]*,[^,]*,\s*(\d+)\s*,', 'WeakAmount'),
    # Vulnerable
    (r'PowerCmd\.Apply<VulnerablePower>\([^,]*,[^,]*,\s*(\d+)\s*,', 'VulnerableAmount'),
    # Strength
    (r'PowerCmd\.Apply<StrengthPower>\([^,]*,[^,]*,\s*(\d+)\s*,', 'StrengthAmount'),
    # Draw
    (r'CardPileCmd\.Draw\([^,]*,\s*(\d+)\s*,', 'DrawAmount'),
    # Hit count
    (r'\.WithHitCount\(\s*(\d+)\s*\)', 'HitCount'),
    # Generic PowerCmd.Apply with numeric amount (e.g. StepGambit, CanYouSeeMe)
    (r'PowerCmd\.Apply<([A-Za-z0-9_]+)>\([^,]*,[^,]*,\s*(\d+)\s*,', 'PowerAmount_{0}'),
]

# Extract canonical vars already defined.
var_re = re.compile(
    r'new\s+(IntVar|DamageVar|BlockVar|EnergyVar|MagicNumberVar|PowerVar<[^>]+>)\s*\(\s*(?:"([^"]+)"\s*,\s*)?([^)]+)\)'
)

for cs_file in sorted(card_dir.glob('flametail*.cs')):
    text = cs_file.read_text(encoding='utf-8')
    class_name = cs_file.stem

    # Existing vars
    existing = []
    for m in var_re.finditer(text):
        vtype, vname, rest = m.groups()
        if vname is None:
            # DamageVar/BlockVar default names
            if 'Damage' in vtype:
                vname = 'Damage'
            elif 'Block' in vtype:
                vname = 'Block'
            elif 'Energy' in vtype:
                vname = 'Energy'
            else:
                vname = vtype
        existing.append((vtype, vname, rest.strip()))

    # Find numeric literals
    found = []
    for pat, default_name in patterns:
        for m in re.finditer(pat, text):
            if 'PowerAmount_' in default_name:
                power_type = m.group(1)
                value = int(m.group(2))
                name = default_name.format(power_type)
            else:
                value = int(m.group(1))
                name = default_name
            found.append((name, value))

    # Hardcoded Footwork removal via ModifyAmount(-1)
    for m in re.finditer(r'PowerCmd\.ModifyAmount\([^,]*,\s*footwork\s*,\s*-1\s*,', text):
        found.append(('FootworkRemoved', 1))

    if existing or found:
        print(f"\n{class_name}")
        if existing:
            print("  Existing vars:", existing)
        if found:
            print("  Hardcoded effect numbers:", found)
