using System;
using Cysharp.Threading.Tasks;
using GF;
using UnityEngine;

namespace BPGame
{
    public class Root: MonoBehaviour
    {
        // [BPGame] 根节点启动入口
        private void Start()
        {
            // 设置应用程序目标帧率
            // Application.targetFrameRate = 60;
            
            Debug.Log($"<color=#88ff00>=== [{nameof(BPGame)}] Root Start Success! ===</color>\n<color=yellow>开始编写项目代码吧！</color>");
            // TODO: 开始你的第一行代码！
            
            // TODO: 打开第一个界面
            // App.UI.OpenAsync<LaunchPageView>().Forget();
        }
    }
}