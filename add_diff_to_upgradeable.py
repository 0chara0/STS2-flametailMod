import json
from pathlib import Path

loc_dir = Path('flametail/localization')

# Card suffix -> replacements in description and smartDescription.
# Each value is a dict of old substring -> new substring.
REPLACEMENTS = {
    'FLAMETAIL_PRE_FIGHT_WARMUP': {'{Footwork}': '{Footwork:diff()}'},
    'FLAMETAIL_REGAIN_POSTURE': {'{Footwork}': '{Footwork:diff()}'},
    'FLAMETAIL_IMPROVISE_FORESIGHT': {'{Footwork}': '{Footwork:diff()}'},
    'FLAMETAIL_CAN_YOU_SEE_ME': {'{Amount}': '{Amount:diff()}'},
    'FLAMETAIL_VALOR': {'{Amount}': '{Amount:diff()}'},
    'FLAMETAIL_PARRYING_DAGGER': {'{Amount}': '{Amount:diff()}'},
    'FLAMETAIL_TENACITY': {'{Amount}': '{Amount:diff()}'},
}

for lang in ('eng', 'zhs'):
    path = loc_dir / lang / 'cards.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    for suffix, reps in REPLACEMENTS.items():
        for key_suffix in ('description', 'smartDescription'):
            key = f'FLAMETAIL_CARD_{suffix}.{key_suffix}'
            if key not in data:
                continue
            for old, new in reps.items():
                data[key] = data[key].replace(old, new)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Updated {path}')
