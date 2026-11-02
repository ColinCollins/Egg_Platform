from pathlib import Path
from copy import copy
import re,json,hashlib,openpyxl
r=Path('D:/GitStore/BrinPunk/BrainPunk');t=Path(__file__).parent;checks=[]
def read(p):return (r/p).read_text(encoding='utf-8-sig')
def check(ok,name):
    assert ok,name;checks.append(name)
def dig(s):return s.replace('DrawGame_ChoiceLevelPanel','DigGame__ChoiceLevelPanel').replace('Draw','Dig').replace('draw','dig').replace('20999','40999').replace('DLevel_999','DigLevel_999').replace('画线','挖洞')
pairs=[]
for p in (r/'Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame').glob('*.cs'):
    pairs.append((p.relative_to(r),Path('Assets/Game/UI/LevelChoicePanel/Scripts/DigGame')/dig(p.name)))
for src,dst in [('ChoiceLevelPopup/Resources/DrawGame_ChoiceLevelPanel.prefab','ChoiceLevelPopup/Resources/DigGame__ChoiceLevelPanel.prefab'),('ChoiceLevelPopup/Prefabs/DrawGame_LevelChoiceItem.prefab','ChoiceLevelPopup/Prefabs/DigGame_LevelChoiceItem.prefab'),('Prefabs/DrawGame_ChoiceLevelStarGroup.prefab','Prefabs/DigGame_ChoiceLevelStarGroup.prefab')]:pairs.append((Path('Assets/Game/UI/DrawGame')/src,Path('Assets/Game/UI/DigGame')/dst))
guidmap={}
for src,dst in pairs:
    a=re.search(r'guid: (\w+)',read(str(src)+'.meta')).group(1);b=re.search(r'guid: (\w+)',read(str(dst)+'.meta')).group(1)
    check(a!=b,f'独立 GUID：{dst.name}');guidmap[a]=b
for src,dst in pairs:
    expected=dig(read(src))
    for a,b in guidmap.items():expected=expected.replace(a,b)
    check(read(dst)==expected,f'完整结构复制与显式引用：{dst.name}')
    if str(dst).endswith('.cs'):check('DB.DrawGame' not in read(dst),f'Dig 脚本不读取 Draw 档：{dst.name}')
data=json.loads(read('Assets/Game/Configs/tbdigleveldata.json'));sort=json.loads(read('Assets/Game/Configs/tbdiglevelsort.json'))
check(len(data)==31 and len(sort)==30,'30 普通配置与真实 TBC 行')
draw=json.loads(read('Assets/Game/Configs/tbdrawleveldata.json'))[0]
check(all(set(row)==set(draw) for row in data),'Draw/Dig LevelData 序列化字段完全同构')
check([x['id'] for x in data[:30]]==[str(i) for i in range(40001,40031)],'普通 ID 连续独立')
check([x['path'] for x in sort]==[str(i) for i in range(40001,40031)],'排序按真实配置 ID 关联')
check(data[-1]['id']=='40999' and data[-1]['lockType']==2,'TBC 顺序锁')
globalrow=json.loads(read('Assets/Game/Configs/lubanconfig_tbglobalconst.json'))[0]
yaml=read('Assets/Guru/guru_spec.yaml');raw=re.search(r"^    diggame_config: '(.*)'$",yaml,re.M).group(1);default=json.loads(raw)
keys={x for x in globalrow if x.startswith('digGame')}
check(len(keys)==14 and keys==set(default)-{'enabled','level_sort'},'14 个独立云控字段完整同步')
check(all(default[x]==globalrow[x] for x in keys),'Guru 默认与 Luban 默认一致')
check(default['level_sort']==list(range(40001,40031)),'Guru 本地排序默认闭合')
gen=read('Assets/Guru/Gen/GuruAllSpecs.G.cs')
check('["diggame_config"] = "'+raw.replace('"','\\"')+'",' in gen,'Guru 生成输出遵循现行模板')
rc=read('Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs')
check('private const int ForceFetchMaxAttempts = 1;' in rc and 'RemoteConfigMaxWaitSeconds = 5f;' in rc and 'StartupForceFetchMaxWaitSeconds = 10f;' in rc,'ForceFetch 次数与两个等待预算保留')
check('TryApplyPendingDigConfig(this)' in read('Assets/Game/Scripts/GameManager.cs'),'Dig 延迟提交由既有 Update 驱动')
for key in keys:
    check(key in read('Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs') and key in read('Assets/Game/Editor/LubanGenerator.cs'),f'字段读取和生成白名单：{key}')
for name,path in [('LevelData.xlsx','Luban/Configs/Datas/LevelData.xlsx'),('LevelSort.xlsx','Luban/Configs/Datas/LevelSort.xlsx'),('Global.xlsx','Luban/Configs/Datas/Const/Global.xlsx')]:
    a=openpyxl.load_workbook(t/'baseline'/name);b=openpyxl.load_workbook(r/path)
    for sheet in a:
        if name=='LevelData.xlsx' and sheet.title=='dig_level':continue
        dst=b[sheet.title]
        for row in sheet:
            for c in row:
                if c.value is None:continue
                d=dst[c.coordinate]
                check(c.value==d.value and c.data_type==d.data_type,f'{name}/{sheet.title}/{c.coordinate}原内容')
                assert all(copy(getattr(c,p))==copy(getattr(d,p)) for p in ['font','fill','border','alignment','protection','number_format']),(name,sheet.title,c.coordinate,'样式变化')
        check(set(str(x) for x in sheet.merged_cells)==set(str(x) for x in dst.merged_cells),f'{name}/{sheet.title}合并单元格')
    check(True,f'{name}旧单元格值/类型/字体/填充/边框/对齐/保护/数字格式保留')
report={'count':len(checks),'checks':checks,'resource_gap':[x['path'] for x in data if not (r/'Assets/Game/Prefabs/DigLevel'/(x['path']+'.prefab')).exists()]}
(t/'static-checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'PASS {len(checks)} static/config/workbook checks. Missing ordinary prefabs: {len(report["resource_gap"])}.')
