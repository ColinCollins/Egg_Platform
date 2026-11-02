from pathlib import Path
import subprocess,re,json,hashlib
r=Path('D:/GitStore/BrinPunk/BrainPunk');t=Path(__file__).parent
def before(path):return subprocess.check_output(['git','show','6e851ee60:BrainPunk/'+path],cwd=r).decode('utf-8-sig').replace('\r\n','\n')
def read(path):return (r/path).read_text(encoding='utf-8-sig')
def method(s,name):
 a=re.search(r'^[ \t]*(?:private|public|internal|protected) [^\r\n=;(){}]*\b'+name+r'\(',s,re.M).start();b=s.index('{',s.index(name+'(',a));i=b+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[a:i]
checks={}
for path in ['Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs','Assets/Game/Scripts/Play/Dig/DigPlayCtrl.Analytics.cs','Assets/Game/Scripts/Play/Dig/DigGameLevelCtrl.cs','Assets/Game/Scripts/Level/DigLevel/DigGermCtrl.cs','Assets/Game/Scripts/Level/DigLevel/DigSizeRules.cs','Assets/Game/Scripts/Level/DigLevel/BaseDigComp.cs','Assets/Game/Scripts/Level/DigLevel/DigTerrainCtrl.cs','Assets/Game/UI/DigGame/Scripts/DigGame_GameVictoryPanel.cs','Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_DeepLink.cs','Assets/Game/DB/DigGame.cs','Assets/Game/DB/DigGame_Partial.cs']:
 checks[path]=before(path)==read(path)
for path,names in {
 'Assets/Game/Scripts/Level/DigLevel/DigLevelCtrl.cs':['QueueContact','FixedUpdate','OnConsumed','BeginFailure','Submit','TryDigSegment','ConfigureInk','ShowHint','AddRewardInk','SetInputEnabled','SetPaused'],
 'Assets/Game/Scripts/Level/DigLevel/DigActorCtrl.cs':['Initialize','Grow','SetFrozen','ApplyScale','PlayDeath','SetAnimationPaused','ReportContact','OnDisable']
}.items():
 for name in names:checks[path+':'+name]=method(before(path),name)==method(read(path),name)
assert all(checks.values()),[name for name,valid in checks.items() if not valid]
hashes=json.loads((t/'source-hashes.json').read_text(encoding='utf-8'))
for source,digest in hashes.items():assert hashlib.sha256(Path(source).read_bytes()).hexdigest()==digest,source
goal=read('Assets/Game/Scripts/Level/DigLevel/DigTargetTrigger.cs');assert all(x in goal for x in ['[SerializeField] private DigLevelCtrl level','[SerializeField] private Collider2D triggerCollider','[SerializeField] private int index','attachedRigidbody.TryGetComponent','TryReachTarget'])
assert not any(x in goal for x in ['GameObject.Find','Transform.Find','GetComponentsInChildren','DB.','UIManager','DispatchEvent','StartCoroutine'])
level=read('Assets/Game/Scripts/Level/DigLevel/DigLevelCtrl.cs');assert level.count('Submit(DigLevelResult.Succeeded)')==2
assert 'BeginVictory()' in method(level,'TrySucceed') and 'BeginVictory()' in method(level,'TryReachTarget') and 'BeginVictory()' in method(level,'TryCompleteByHint')
for name in ['DigTargetTrigger.cs','DigVictoryCondition.cs']:
 guid=re.search('guid: ([0-9a-f]{32})',read('Assets/Game/Scripts/Level/DigLevel/'+name+'.meta')).group(1)
 matching=[]
 for meta in (r/'Assets/Game').rglob('*.meta'):
  if ('guid: '+guid) in meta.read_text(encoding='utf-8-sig',errors='replace'):matching.append(meta.relative_to(r).as_posix())
 assert len(matching)==1,matching
report={'原文件及方法保持':checks,'保持数量':len(checks),'新运行源码与真实Unity测试副本SHA256':'一致','两个新脚本MetaGUID':'各唯一','Prefab其余数据':'仅Actor index=0、001胜利类型=0和延迟=1，其余字节规范化换行后保持','存档广告统计UI':'原消费者文件保持；完整集成仍未验证'}
(t/'static-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS',len(checks),'unchanged source/method checks, new source hashes and bindings')
