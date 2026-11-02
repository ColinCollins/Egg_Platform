from pathlib import Path
import json,shutil,hashlib,subprocess
support=Path(__file__).parent;t=Path('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-victory-conditions-20261012');t.mkdir(exist_ok=True);r=Path('D:/GitStore/BrinPunk/BrainPunk');u=t/'unity'
for name in ['Assets/Runtime','Assets/Editor','Assets/Plugins','Assets/ProbeAssets','Packages','ProjectSettings']:(u/name).mkdir(parents=True,exist_ok=True)
names=['BaseDigComp.cs','DigActorCtrl.cs','DigGermCtrl.cs','DigLevelCtrl.cs','IDigLevelRuntime.cs','DigSizeRules.cs','DigVictoryCondition.cs','DigTargetTrigger.cs']
hashes={}
for name in names:
 p=r/'Assets/Game/Scripts/Level/DigLevel'/name;dest=u/'Assets/Runtime'/name;shutil.copy2(p,dest);hashes[str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
 if p.with_suffix('.cs.meta').exists():shutil.copy2(p.with_suffix('.cs.meta'),dest.with_suffix('.cs.meta'))
source=(r/'Assets/Game/Scripts/Level/DrawLevel/DrawLevelRuntimeTypes.cs').read_text(encoding='utf-8-sig');a=source.index('public readonly struct DrawInkState');b=source.index('{',a);i=b+1;depth=1
while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
(u/'Assets/Runtime/DrawInkState.cs').write_text('using System;\nusing UnityEngine;\n'+source[a:i],encoding='utf-8')
shutil.copy2(r/'Assets/GF/Third/Demigiant/DOTween/DOTween.dll',u/'Assets/Plugins/DOTween.dll')
for ext in ['.prefab','.prefab.meta']:shutil.copy2(r/('Assets/Game/Prefabs/DigLevel/DigActor'+ext),u/('Assets/ProbeAssets/DigActor'+ext))
(u/'Assets/Runtime/BoundaryStubs.cs').write_text('''using UnityEngine;
namespace Game.Dig {public sealed class DigTerrainCtrl:MonoBehaviour {public float StrokeStep=>.075f;public void BindRuntime(){}public bool ContainsPoint(Vector2 p)=>false;public bool Stamp(Vector2 p)=>false;public void FlushCollider(){}}}
namespace UnityEngine.EventSystems {public class EventSystem:MonoBehaviour {public static EventSystem current;public bool IsPointerOverGameObject(int id)=>false;}}
''',encoding='utf-8')
dependencies=json.loads((r/'Packages/manifest.json').read_text(encoding='utf-8-sig'))['dependencies']
(u/'Packages/manifest.json').write_text(json.dumps({'dependencies':{name:value for name,value in dependencies.items() if name.startswith('com.unity.modules.')}},indent=2),encoding='utf-8')
(u/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.0.59f2\n',encoding='utf-8')
(u/'Assets/Editor/VictoryEntry.cs').write_text('''using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class VictoryEntry {public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var host=new GameObject("VictoryVerification");host.AddComponent<DigVictoryVerification>();EditorSceneManager.SaveScene(host.scene,"Assets/Probe.unity");EditorApplication.isPlaying=true;}}
''',encoding='utf-8')
if (support/'unity-fixture/VictoryEntry.cs').exists():shutil.copy2(support/'unity-fixture/VictoryEntry.cs',u/'Assets/Editor/VictoryEntry.cs')
if (support/'unity-fixture/DigVictoryVerification.cs').exists():shutil.copy2(support/'unity-fixture/DigVictoryVerification.cs',u/'Assets/DigVictoryVerification.cs')
(t/'source-hashes.json').write_text(json.dumps(hashes,ensure_ascii=False,indent=2),encoding='utf-8')
checks={}
for path in ['Assets/Game/Prefabs/DigLevel/DigActor.prefab','Assets/Game/Prefabs/DigLevel/DigLevel_001.prefab']:
 before=subprocess.check_output(['git','show','6e851ee60:BrainPunk/'+path],cwd=r).decode('utf-8-sig').replace('\r\n','\n');now=(r/path).read_text(encoding='utf-8-sig')
 stripped=now.replace('  index: 0\n','',1) if path.endswith('DigActor.prefab') else now.replace('  victoryCondition: 0\n  victoryDelay: 1\n','',1)
 checks[path]=stripped==before
assert all(checks.values()),checks
(t/'prefab-check.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared dedicated Unity sandbox; real physics/Tween, terrain and UI boundaries stubbed')
