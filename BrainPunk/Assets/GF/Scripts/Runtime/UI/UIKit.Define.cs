using System;
using System.Collections.Generic;

namespace GF
{
    public enum UILayer
    {
        //场景层（可用于场景内的UI）
        SceneLayer,
        //基础层（可用于全屏界面）
        BaseLayer,
        //中间层（可用于Loading，在Base之上，在弹窗之下）
        MiddleLayer,
        //弹出层（可用于弹窗）
        PopupLayer,
        //顶层（可用于新手引导）
        TopLayer,
        //通知层（可用于断线提示）
        NotifyLayer
    }
}