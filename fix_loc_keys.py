import json
import re
from pathlib import Path

card_dir = Path('flametailCode/Cards')
localization_dir = Path('flametail/localization')

# Build a mapping from the concatenated upper ID to the snake-case upper ID.
# Class names look like "flametailSwiftStrike"; we strip the "flametail" prefix.
replacement_map: dict[str, str] = {}

for cs_file in sorted(card_dir.glob('flametail*.cs')):
    class_name = cs_file.stem
    if not class_name.startswith('flametail'):
        continue

    suffix = class_name[len('flametail'):]  # e.g. "SwiftStrike"
    if not suffix:
        continue

    old_id = 'FLAMETAIL_' + suffix.upper()  # e.g. "FLAMETAIL_SWIFTSTRIKE"
    # Insert underscores between camel-case words.
    snake = re.sub(r'(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])', '_', suffix)
    new_id = 'FLAMETAIL_' + snake.upper()  # e.g. "FLAMETAIL_SWIFT_STRIKE"

    if old_id != new_id:
        replacement_map[old_id] = new_id

print(f"Found {len(replacement_map)} card IDs needing underscore conversion:")
for old, new in sorted(replacement_map.items()):
    print(f"  {old} -> {new}")


def fix_json(path: Path) -> int:
    with path.open('r', encoding='utf-8') as f:
        data = json.load(f)

    new_data: dict[str, str] = {}
    changed = 0

    for key, value in data.items():
        new_key = key
        if key.startswith('FLAMETAIL_CARD_') and ('.title' in key or '.description' in key or '.smartDescription' in key):
            # Suffix part between the prefix and the final .xxx component.
            suffix_part = key[len('FLAMETAIL_CARD_'):]
            # Remove the trailing .title / .description / .smartDescription
            for trailing in ('.title', '.description', '.smartDescription'):
                if suffix_part.endswith(trailing):
                    suffix_part = suffix_part[:-len(trailing)]
                    break

            if suffix_part in replacement_map:
                new_key = 'FLAMETAIL_CARD_' + replacement_map[suffix_part] + key[key.rfind('.'):]
                if new_key != key:
                    changed += 1

        new_data[new_key] = value

    if changed:
        with path.open('w', encoding='utf-8') as f:
            json.dump(new_data, f, ensure_ascii=False, indent=2)
            f.write('\n')

    return changed


for lang in ('eng', 'zhs'):
    path = localization_dir / lang / 'cards.json'
    n = fix_json(path)
    print(f"{path}: updated {n} keys")
