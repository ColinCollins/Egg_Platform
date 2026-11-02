#nullable enable
using System;
using GF;
using UnityEngine;
using UnityEngine.UI;

namespace BPGame
{
    [UIViewAttribute(UILayer.SceneLayer, "Assets/BPGame/Bundles/UI/MainPageView.prefab", false)]
    public class MainPageView : UIView
    {
        #region 自动生成代码，勿修改

		private Text txt_title;
		private Button btn_back;

		protected override void ScriptGenerator()
		{
			txt_title = FindChildComponent<Text>("root/txt_title");
			btn_back = FindChildComponent<Button>("root/btn_back");
		}

		#endregion
        
        public Action? OnClickBack;

        public string TitleText
        {
            get => txt_title.text;
            set => txt_title.text = value;
        }
        
        public override BaseLogic CreateLogic(params object[] args)
        {
        	return new MainPageLogic(this);
        }
        
        /// <summary>
        /// 首次打开界面调用
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
        }
        
        /// <summary>
        /// 添加响应事件，在OnEnter之前调用
        /// </summary>
        public override void AddEvent()
        {
            base.AddEvent();
            btn_back.onClick.AddListener(OnClickBackEvent);
        }
        
        /// <summary>
        /// 二次打开界面调用
        /// </summary>
        public override void OnRefresh()
        {
            base.OnRefresh();
        }
        
        /// <summary>
        /// 退出界面调用
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
        }
        
        /// <summary>
        /// 销毁界面调用
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
        }
        
        
        #region ButtonEvent

        private void OnClickBackEvent()
        {
            OnClickBack?.Invoke();
        }
        

        #endregion
    }
}