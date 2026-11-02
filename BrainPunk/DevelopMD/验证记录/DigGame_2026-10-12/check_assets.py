from pathlib import Path
import re,collections,json
root=Path('D:/GitStore/BrinPunk/BrainPunk')
task=Path('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-base-20261012')
files=list((root/'Assets/Game/UI/DigGame').rglob('*.prefab'))+[root/'Assets/Game/Prefabs/DigLevel/DigLevel_001.prefab',root/'Assets/Game/Prefabs/DigLevel/DigLevel_999.prefab']
checks=[]
def check(c,n):
    checks.append({'check':n,'passed':bool(c)})
    if not c: print('FAIL: '+n)
for p in files:
    s=p.read_text(encoding='utf-8-sig'); name=str(p.relative_to(root))
    anchors=re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M)
    check(len(anchors)==len(set(anchors)),name+' anchors unique')
    refs=re.findall(r'\{fileID: (-?\d+)\}',s)
    missing=set(refs)-set(anchors)-{'0'}
    check(not missing,name+' local fileID closure '+str(missing))
    check('m_Script: {fileID: 11500000, guid: 00000000000000000000000000000000' not in s,name+' script guid nonzero')
hud=root/'Assets/Game/UI/DigGame/GamePlayPanel/Resources/DigGame_GamePlayPanel.prefab'
s=hud.read_text(encoding='utf-8')
check('442589a9b5724f0493b97ff461e241d0' in s,'HUD binds preserved Dig script GUID')
check('inkRoot: {fileID:' in s and '  inkProcessHandle:' in s,'HUD explicit ink bindings')
check('d5d94497926d4024b274cd0cb12b735f' in Path(str(hud)+'.meta').read_text(encoding='utf-8-sig'),'HUD resource GUID preserved')
check(not (root/'Assets/Game/UI/DigGame/Resources/DigGame_GamePlayPanel.prefab').exists(),'HUD Resources name unique after move')
generated=(root/'Assets/Game/UI/DigGame/GamePlayPanel/Scripts/DigGame_GamePlayPanel.Generated.cs').read_text(encoding='utf-8-sig')
drawgenerated=(root/'Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.Generated.cs').read_text(encoding='utf-8-sig').replace('DrawGame_GamePlayPanel','DigGame_GamePlayPanel')
check(generated==drawgenerated,'HUD Generated exact copied output for unchanged named hierarchy')
for name in ['pencilShop_btn','cd_txt']:
    b=next(b for b in re.split(r'(?=^--- !u!)',s,flags=re.M) if f'  m_Name: {name}\n' in b)
    check('m_IsActive: 0' in b,'HUD '+name+' inactive')
tbc=(root/'Assets/Game/Prefabs/DigLevel/DigLevel_999.prefab').read_text(encoding='utf-8-sig')
for old in ['6f5c49a0b4214ed9a371d22344a31901','7390537236784c0384b1f84839bd3893','ce56a707b1334502b17ebc72ecb11559','10877d0fc30026040bef4d574b8ebaf0','ff88faa2077934b46bbfb5ba30564e2f']:
    check(old not in tbc,'TBC removes old runtime dependency '+old)
check('Game.Dig.DigLevel999Ctrl, Assembly-CSharp' in tbc,'TBC portal UnityEvent target type')
variant=(root/'Assets/Game/UI/DigGame/TipsPopup/Resources/DigGame_GameTipsPopup_2.prefab').read_text(encoding='utf-8-sig')
base=root/'Assets/Game/UI/DigGame/TipsPopup/Resources/DigGame_GameTipsPopup.prefab'
g=re.search(r'^guid: (\w+)',Path(str(base)+'.meta').read_text(encoding='utf-8-sig'),re.M).group(1)
check(g in variant and 'propertyPath: _isPassLevelPopup\n      value: 1' in variant,'Skip variant inherits Dig popup and enables skip flag')
# 精确核对所有新增 GUID 在当前 Assets 的唯一性；Unity/Bear/GF 只读枚举。
index=collections.defaultdict(list)
for m in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: (\w+)',m.read_text(encoding='utf-8',errors='replace'),re.M)
    if match: index[match.group(1)].append(str(m))
for p in [*files,root/'Assets/Game/UI/DigGame/GamePlayPanel/Scripts/DigGame_GamePlayPanel.cs',root/'Assets/Game/UI/DigGame/TipsPopup/Scripts/DigGame_GameTipsPopup.cs',root/'Assets/Game/Scripts/Level/DigLevel/Level999/DigLevel999Ctrl.cs']:
    m=Path(str(p)+'.meta'); g=re.search(r'^guid: (\w+)',m.read_text(encoding='utf-8-sig'),re.M).group(1)
    check(len(index[g])==1,str(p.relative_to(root))+' GUID unique')
unknown=collections.defaultdict(list)
for p in files:
    for g in set(re.findall(r'guid: ([0-9a-f]{32})',p.read_text(encoding='utf-8'))):
        if g not in index and not g.startswith('0000000000000000'): unknown[g].append(str(p.relative_to(root)))
result={'checks':checks,'unknownExternalGuids':dict(unknown)}
(task/'asset-checks.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{sum(c["passed"] for c in checks)}/{len(checks)} checks passed; external GUIDs not in Assets (package/builtin candidates): {len(unknown)}')
if any(not c['passed'] for c in checks): raise SystemExit(1)
