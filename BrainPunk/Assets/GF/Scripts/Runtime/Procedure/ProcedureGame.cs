using System.Linq;
#if !UNITY_EDITOR
using System.Reflection;
#endif
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace GF
{
    public class ProcedureGame: FsmState<App>
    {
        public override void OnEnter(params object[] args)
        {
            base.OnEnter(args);
            StartUp().Forget();
        }

        private async UniTask StartUp()
        {
            GameObject root = await App.Res.LoadAssetAsync<GameObject>($"Assets/{App.Instance.startUpAppName}/Bundles/Root/Root.prefab", "ProcedureGame");
            GameObject.Instantiate(root);
        }
    }
}