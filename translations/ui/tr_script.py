
import json, re, sys
sys.stdout = open(1, 'w', encoding='utf-8', closefd=False)

# Load data
with open('tr_glossary.json', 'r', encoding='utf-8') as f: glossary = json.load(f)
with open('tr_profiles.json', 'r', encoding='utf-8') as f: profiles = json.load(f)
with open('tr_lookups.json', 'r', encoding='utf-8') as f: lookups = json.load(f)
with open('TskNames.json', 'r', encoding='utf-8') as f: tn = json.load(f)

# Sort glossary by length descending
sorted_glossary = sorted(glossary.items(), key=lambda x: len(x[0]), reverse=True)

def translate_text(text):
    result = text
    for jp, cn in sorted_glossary:
        result = result.replace(jp, cn)
    return result

def translate_profile(text):
    result = translate_text(text)
    for jp, cn in profiles.items():
        result = result.replace(jp, cn)
    return result

ct = lookups['committee']
cl = lookups['club']
hb = lookups['hobby']
sn = lookups['specific_name']
un = lookups['unit_name']
eu = lookups['exclusive_unit']

with open('_batches/unit_10of11.jsonl', 'r', encoding='utf-8') as f:
    lines = f.readlines()

output = []
for line in lines:
    stripped = line.strip()
    if not stripped:
        output.append(line)
        continue
    entry = json.loads(stripped)
    field = entry['field']
    source = entry['source']
    translation = ''

    if field == 'birthday':
        translation = source
    elif field == 'character_name':
        translation = tn.get(source, source)
    elif field == 'character_name_kana':
        translation = source
    elif field == 'club':
        translation = cl.get(source, translate_text(source))
    elif field == 'committee':
        translation = ct.get(source, translate_text(source))
    elif field == 'cv':
        translation = source
    elif field == 'exclusive_unit_name':
        translation = eu.get(source, translate_text(source))
    elif field == 'full_name':
        if source in tn:
            translation = tn[source]
        else:
            parts = re.split(r'(<style=[^>]+>[^<]*</style>)', source)
            tparts = []
            for part in parts:
                if part.startswith('<style'):
                    tparts.append(part)
                else:
                    tparts.append(tn.get(part, part))
            translation = ''.join(tparts)
    elif field == 'guardian_star':
        translation = source
    elif field == 'hobby':
        translation = hb.get(source, translate_text(source))
    elif field == 'profile':
        translation = translate_profile(source)
    elif field == 'specific_detail':
        translation = translate_text(source)
    elif field == 'specific_name':
        translation = sn.get(source, translate_text(source))
    elif field == 'unit_name':
        key = entry['identity'].get('character_id','') + ':' + entry['identity'].get('unit_id','')
        translation = un.get(key, translate_text(source))

    if translation:
        entry['translation'] = translation
        if entry.get('status') in ('new', 'stale'):
            entry['status'] = 'translated'

    output.append(json.dumps(entry, ensure_ascii=False) + '\n')

with open('_batches/unit_10of11.jsonl', 'w', encoding='utf-8') as f:
    f.writelines(output)

done = sum(1 for l in output if json.loads(l.strip()).get('translation'))
total = sum(1 for l in output if l.strip())
print(f'Done! {done}/{total} entries translated.')
