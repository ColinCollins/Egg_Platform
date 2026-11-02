from pathlib import Path
import shutil,re,json
repo=Path('D:/GitStore/BrinPunk/BrainPunk')
task=Path('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-base-20261012')
project=task/'unity'
def copy(p):
    src=repo/p; dst=project/p; dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(src,dst)
    if Path(str(src)+'.meta').exists(): shutil.copy2(str(src)+'.meta',str(dst)+'.meta')
for folder in ['Assets/Editor','Assets/Plugins','Packages','ProjectSettings']: (project/folder).mkdir(parents=True,exist_ok=True)
(project/'Packages/manifest.json').write_text(json.dumps({'dependencies':{('com.unity.modules.'+n):'1.0.0' for n in ['physics2d','tilemap','imgui','audio','ui','uielements','jsonserialize','particlesystem','animation','imageconversion']}},indent=2),encoding='utf-8')
(project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.0.59f2\n',encoding='utf-8')
core='Assets/Game/Scripts/Level/DigLevel'
for p in (repo/core).glob('*.cs'): copy(p.relative_to(repo))
copy('Assets/Game/Scripts/Level/DrawLevel/DrawLevelRuntimeTypes.cs')
copy('Assets/Game/Scripts/Effects/SceneSpriteBrushClear.cs')
for p in (repo/'Assets/Game/ArtSrc/Dig').iterdir():
    if not p.name.endswith('.meta'): copy(p.relative_to(repo))
for name in ['MaskRTSprite.shader','MaskRTStamp.shader']: copy('Assets/Game/ArtSrc/Shader/Sprites/'+name)
for name in ['DigLevel_001.prefab','DigActor.prefab','DigGerm.prefab']: copy('Assets/Game/Prefabs/DigLevel/'+name)
for dll in ['UniTask.dll','Unity.TextMeshPro.dll','UnityEngine.UI.dll','DOTween.Modules.dll']:
    shutil.copy2(repo/'Library/ScriptAssemblies'/dll,project/'Assets/Plugins'/dll)
shutil.copy2(repo/'Assets/GF/Third/Demigiant/DOTween/DOTween.dll',project/'Assets/Plugins/DOTween.dll')
stubs=(repo/'DevelopMD/验证记录/DigGame_2026-09-29/EventStubs.cs').read_text(encoding='utf-8-sig')
(project/'Assets/EventStubs.cs').write_text(stubs,encoding='utf-8')
test=(repo/'DevelopMD/验证记录/DigGame_2026-09-30/DigVerification.cs').read_text(encoding='utf-8-sig')
test=test.replace('void Fresh()','void Fresh(float ink=20f)').replace('level.Resolved+=r=>results.Add(r);level.InitializeRuntime();','Set(level,"maxInkLength",ink);level.Resolved+=r=>results.Add(r);level.InitializeRuntime();')
marker='        Check(DigSizeRules.CanConsumeBatch(10,new[]{5}),"小于");'
newtests='''        Fresh();
        Check(level.InkState.MaxInk==20f && level.InkState.UsedInk==0f && level.InkState.StarCount==3,"出生空腔免费/初始三星");
        Check(level.HasHint,"首关绑定提示");
        level.ShowHint(true);Check(Field<GameObject[]>(level,"hintObjects")[0].activeSelf,"显示本关提示");
        level.ShowHint(false);Check(!Field<GameObject[]>(level,"hintObjects")[0].activeSelf,"隐藏提示");
        int inkEvents=0;level.InkChanged+=s=>inkEvents++;
        Check(level.TryDigSegment(new Vector2(2,2),new Vector2(2,2),true),"点按盖章");
        Check(Mathf.Abs(level.InkState.UsedInk-.075f)<.0001f,"首章耗一个间距避免免费点按");
        Check(!level.TryDigSegment(new Vector2(2,2),new Vector2(2,2),false),"静止不持续耗墨");
        Check(!level.TryDigSegment(new Vector2(10,10),new Vector2(12,12),false),"沙土外不耗墨");
        Check(inkEvents==1,"有效输入单次通知");
        Check(level.TryDigSegment(new Vector2(2,2),new Vector2(3,2),false),"真实轨迹挖掘");
        Check(Mathf.Abs(level.InkState.UsedInk-1.075f)<.0001f,"轨迹长度耗墨不依赖采样数");
        level.SetPaused(true);float used=level.InkState.UsedInk;
        Check(!level.TryDigSegment(new Vector2(2,1),new Vector2(3,1),false)&&level.InkState.UsedInk==used,"暂停不可挖掘");
        level.AddRewardInk();Check(Mathf.Abs(level.InkState.CurrentCapacity-40)<.0001f && level.InkState.UsedInk==used,"暂停中奖励加基础容量保持已用长度");
        level.SetPaused(false);level.SetInputEnabled(false);
        Check(!level.TryDigSegment(new Vector2(2,1),new Vector2(3,1),false),"输入锁不可挖掘");
        Fresh(1f);Check(level.TryDigSegment(new Vector2(1,2),new Vector2(3,2),false),"墨水不足裁剪轨迹");
        Check(level.InkState.RemainingInk==0f && Mathf.Abs(level.InkState.UsedInk-1)<.0001f,"耗墨不超容量");
        Check(!level.TryDigSegment(new Vector2(1,1),new Vector2(3,1),false),"耗尽停止挖掘");
        Check(actor.GetComponent<Rigidbody2D>().simulated,"耗尽不停止物理");
        var exhaustedMap=Field<Tilemap>(Field<DigTerrainCtrl>(level,"terrain"),"collisionMap");
        Check(exhaustedMap.HasTile(exhaustedMap.WorldToCell(new Vector2(3.5f,2f))),"裁剪后远端沙土保持");
        level.AddRewardInk();Check(level.TryDigSegment(new Vector2(2,2),new Vector2(2.5f,2),false),"加墨后可继续挖掘");
        Fresh();Set(level,"_usedInk",16f);Check(level.InkState.StarCount==2,"二星阈值含等号");
        Set(level,"_usedInk",10f);Check(level.InkState.StarCount==3,"三星阈值含等号");
        Set(level,"_usedInk",20f);Check(level.InkState.StarCount==1,"耗尽仍有基础一星");
        level.SetPaused(true);Check(!level.TryCompleteByHint(),"暂停时拒绝金币跳关");
        level.SetPaused(false);Check(level.TryCompleteByHint()&&results.Count==1&&results[0]==DigLevelResult.Succeeded,"金币跳关提交成功");
        Check(!level.TryCompleteByHint(),"跳关结果去重");
        Fresh();Check(level.InkState.UsedInk==0f&&level.InkState.CurrentCapacity==20f,"新运行时清理尝试耗墨");
'''
assert marker in test
test=test.replace(marker,newtests+marker)
test=test.replace('../playmode-failed.txt',str(task/'playmode-failed.txt').replace('\\','/')).replace('../playmode-results.txt',str(task/'playmode-results.txt').replace('\\','/'))
(project/'Assets/DigVerification.cs').write_text(test,encoding='utf-8')
(project/'Assets/Editor/DigTestRunner.cs').write_text('''using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DigTestRunner {
 public static void Run() { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); new GameObject("Dig verification").AddComponent<DigVerification>(); EditorApplication.isPlaying=true; }
}
''',encoding='utf-8')
print('Isolated Unity project prepared with current Dig algorithms and prefab. Shared events stubbed; production UI/URP/platform excluded.')
