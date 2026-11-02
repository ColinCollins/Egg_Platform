# GuruRating 组件使用文档

## 📋 目录
1. [概述](#概述)
2. [架构设计](#架构设计)
3. [核心类详解](#核心类详解)
4. [使用指南](#使用指南)
5. [配置说明](#配置说明)
6. [事件流程](#事件流程)
7. [最佳实践](#最佳实践)
8. [常见问题](#常见问题)

---

## 概述

### 组件简介

GuruRating 是 Guru SDK 提供的应用内评分组件，用于收集用户对应用的评分和反馈。组件采用 MVC 架构设计，支持高度自定义，适配 RTL（从右到左）语言布局。

### 功能特性

| 特性 | 说明 |
|------|------|
| ⭐ 星级评分 | 支持 1-5 星评分 |
| 💬 反馈收集 | 低星评分时收集用户反馈 |
| 📧 邮件反馈 | 自动发送反馈邮件到客服邮箱 |
| 🏪 商店引导 | 5星好评时引导到应用商店 |
| 🌍 多语言支持 | 支持 RTL 布局（阿拉伯语、希伯来语等） |
| 🎨 高度自定义 | 可自定义所有 UI 资源和文本 |
| 📱 动画效果 | 流畅的入场/出场动画 |

### 文件位置

```
Packages/
└── com.guru.sdk.uiux.popup/
    └── Runtime/
        └── Rating/
            └── Code/
                ├── RatingController.cs      # 控制器
                ├── RatingSpec.cs            # 配置类
                ├── RatingDefaults.cs        # 默认常量
                ├── UIRatingViewV1.cs        # 视图
                ├── UIRatingStarsV1.cs       # 星星组件
                └── UIRatingStarItemV1.cs    # 星星项
```

---

## 架构设计

### MVC 架构

```
┌─────────────────────────────────────────────────────────────┐
│                        MVC 架构图                            │
└─────────────────────────────────────────────────────────────┘

    ┌─────────────────┐
    │  RatingSpec     │  ← 配置数据（Model）
    │  （配置规格）    │
    └────────┬────────┘
             │
             ▼
    ┌─────────────────┐      控制逻辑      ┌─────────────────┐
    │ RatingController│ ◄────────────────► │  UIRatingViewV1 │
    │   （控制器）     │   更新UI/处理事件   │    （视图）      │
    └────────┬────────┘                   └─────────────────┘
             │
             ▼
    ┌─────────────────┐
    │ UIRatingStarsV1 │  ← 子组件（星星评分）
    │ UIRatingStarItem│
    └─────────────────┘
```

### 状态机

```
┌─────────────────────────────────────────────────────────────┐
│                      状态流转图                              │
└─────────────────────────────────────────────────────────────┘

                    ┌─────────────┐
                    │    Start    │
                    └──────┬──────┘
                           │
                           ▼
    ┌─────────────────────────────────────────┐
    │  RATING_STATE_ENTER (1)                 │
    │  显示评分页面，用户选择星级               │
    │  • 显示 5 个星星供用户选择               │
    │  • 显示标题 "How would you rate us?"    │
    └──────────────────┬──────────────────────┘
                       │
         ┌─────────────┼─────────────┐
         │             │             │
         ▼             ▼             ▼
      1-3 星         4 星          5 星
         │             │             │
         ▼             ▼             ▼
    ┌─────────────────────────────────────────┐
    │  RATING_STATE_FEEDBACK (2)              │
    │  显示反馈输入框                          │
    │  • 显示表情（😢 或 😀）                  │
    │  • 显示反馈标题和信息                    │
    │  • 提供文本输入框                        │
    │  • 显示提交按钮                          │
    └──────────────────┬──────────────────────┘
                       │
                       ▼
    ┌─────────────────────────────────────────┐
    │  RATING_STATE_THANKS (3)                │
    │  显示感谢页面                            │
    │  • 显示感谢图标（👍 或 ❤️）              │
    │  • 显示感谢文本                          │
    │  • 显示确定按钮                          │
    └──────────────────┬──────────────────────┘
                       │
                       ▼
                  ┌─────────┐
                  │  Close  │
                  └─────────┘
```

---

## 核心类详解

### 1. RatingController（控制器）

**命名空间**: `Guru.SDK.UIUX.Popup`

**主要职责**: 控制评分流程、管理UI状态、处理用户交互

#### 核心方法

```csharp
/// <summary>
/// 显示评分弹窗
/// </summary>
/// <param name="spec">配置规格</param>
/// <param name="onEvent">用户操作回调 (星级, 反馈内容)</param>
/// <param name="onClosed">弹窗关闭回调</param>
public void Show(RatingSpec? spec, Action<int, string>? onEvent = null, Action? onClosed = null)

/// <summary>
/// 关闭评分弹窗
/// </summary>
public void Close()
```

#### 完整使用示例

```csharp
using Guru.SDK.UIUX.Popup;
using UnityEngine;

public class RatingExample : MonoBehaviour
{
    [SerializeField] private Transform canvasTransform;
    
    private RatingController _ratingController;
    
    public void ShowRatingPopup()
    {
        _ratingController = new RatingController();
        
        // 配置规格
        var spec = RatingSpec.Default();
        spec.Container = canvasTransform;
        spec.SupportEmail = "support@yourgame.com";
        spec.ShowImmediately = true;
        
        // 显示弹窗
        _ratingController.Show(spec, OnRatingComplete, OnRatingClosed);
    }
    
    /// <summary>
    /// 评分完成回调
    /// </summary>
    /// <param name="stars">用户选择的星级 (1-5)</param>
    /// <param name="message">用户输入的反馈内容</param>
    private void OnRatingComplete(int stars, string message)
    {
        Debug.Log($"用户评分: {stars} 星");
        
        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log($"用户反馈: {message}");
        }
        
        // 根据星级执行不同逻辑
        switch (stars)
        {
            case 5:
                // 引导到应用商店
                OpenAppStore();
                break;
            case 4:
                // 记录好评意向
                Analytics.LogEvent("rating_4_star");
                break;
            default:
                // 记录低星反馈
                Analytics.LogEvent("rating_low_star", new { stars, message });
                break;
        }
    }
    
    private void OnRatingClosed()
    {
        Debug.Log("评分弹窗已关闭");
        _ratingController = null;
    }
    
    private void OpenAppStore()
    {
        #if UNITY_IOS
        Application.OpenURL("itms-apps://itunes.apple.com/app/idYOUR_APP_ID");
        #elif UNITY_ANDROID
        Application.OpenURL("market://details?id=your.package.name");
        #endif
    }
}
```

---

### 2. RatingSpec（配置规格）

**用途**: 定义评分弹窗的所有配置项

#### 属性详解

| 分类 | 属性 | 类型 | 默认值 | 说明 |
|------|------|------|--------|------|
| **容器** | Container | Transform | null | UI 挂载的父节点（通常是 Canvas） |
| **图标资源** | IconClose | string | null | 关闭按钮图标 |
| | IconStarOn | string | null | 星星选中状态图标 |
| | IconStarOff | string | null | 星星未选中状态图标 |
| | ImgPanelBg | string | null | 弹窗背景图 |
| | ImgPanelContentBg | string | null | 内容区域背景 |
| | IconThumb | string | null | 拇指图标（感谢页） |
| | IconHeart | string | null | 爱心图标（感谢页） |
| | IconButtonSubmit | string | null | 提交按钮图标 |
| | IconButtonSubmitOutline | string | null | 提交按钮描边 |
| | IconButtonOk | string | null | 确定按钮图标 |
| | IconButtonOkOutline | string | null | 确定按钮描边 |
| **文本内容** | TitleText | string | "How would you rate us?" | 评分标题 |
| | ThanksTitleText | string | "Thank you" | 感谢标题 |
| | ThanksInfoText | string | "Your support really means a lot." | 感谢信息 |
| | LowStarTitleText | string | "Sorry to hear that" | 低星标题（1-3星） |
| | LowStarInfoText | string | "Please tell us..." | 低星提示信息 |
| | HighStarTitleText | string | "Thank you" | 高星标题（4星） |
| | HighStarInfoText | string | "Please leave your opinion..." | 高星提示信息 |
| | PlaceholderText | string | "Write a comment" | 输入框占位符 |
| | SubmitText | string | "Submit" | 提交按钮文本 |
| | OkText | string | "OK" | 确定按钮文本 |
| **行为控制** | AutoDestroy | bool | true | 关闭后是否自动销毁 |
| | IsRTL | bool | false | 是否从右到左布局 |
| | ShowImmediately | bool | false | 5星时是否立即跳转商店 |
| **邮件反馈** | SupportEmail | string | null | 客服邮箱地址 |
| | MailTitle | string | null | 邮件标题 |
| | MailBody | string | null | 邮件正文模板 |

#### 配置示例

```csharp
// 基础配置
var spec = new RatingSpec
{
    Container = canvasTransform,
    SupportEmail = "support@example.com",
    ShowImmediately = true
};

// 完整自定义配置
var customSpec = new RatingSpec
{
    // 容器
    Container = canvasTransform,
    
    // 图标资源（需要提前加载到 Addressables 或 Resources）
    IconStarOn = "ui/rating/star_on",
    IconStarOff = "ui/rating/star_off",
    ImgPanelBg = "ui/rating/panel_bg",
    IconThumb = "ui/rating/thumb",
    IconHeart = "ui/rating/heart",
    
    // 文本（支持多语言）
    TitleText = Localization.Get("RATING_TITLE"),
    ThanksTitleText = Localization.Get("RATING_THANKS_TITLE"),
    LowStarTitleText = Localization.Get("RATING_LOW_TITLE"),
    HighStarTitleText = Localization.Get("RATING_HIGH_TITLE"),
    
    // 行为
    IsRTL = Localization.IsRTL,
    AutoDestroy = true,
    ShowImmediately = true,
    
    // 邮件
    SupportEmail = "support@yourgame.com",
    MailTitle = "Game Feedback"
};
```

---

### 3. RatingDefaults（默认常量）

**用途**: 定义组件使用的默认资源路径

```csharp
public static class RatingDefaults
{
    // Prefab 路径（相对于 Addressables 或 Resources）
    public const string RatingViewPath = "rating/guru_rating";
    
    // 默认图标资源名
    public const string IconClose = "ic_close";
    public const string RatingIconStarOn = "ic_rating_star_on";
    public const string RatingIconStarOff = "ic_rating_star_off";
    public const string RatingIconThumb = "ic_rating_thumb";
    public const string RatingImagePopupBackground = "rating_popup_background";
    public const string RatingPanelBg = "rating_panel_bg";
    public const string RatingPanelContentBg = "rating_panel_content_bg";
}
```

---

### 4. UIRatingViewV1（视图组件）

**用途**: 绑定和管理所有 UI 元素

#### 主要组件

```csharp
public class UIRatingViewV1 : MonoBehaviour
{
    // 根节点
    public RectTransform rootRect;           // 弹窗根节点
    
    // 页面节点（状态切换）
    public CanvasGroup nodeRating;           // 评分页面
    public CanvasGroup nodeLowStar;          // 低星反馈页面
    public CanvasGroup nodeThanks;           // 感谢页面
    
    // 星星组件
    public UIRatingStarsV1 uiStars1;         // 第一组星星（评分页）
    public UIRatingStarsV1 uiStars2;         // 第二组星星（反馈页）
    
    // 按钮
    public Button closeBtn;                  // 关闭按钮
    public Button submitBtn;                 // 提交按钮
    public Button okBtn;                     // 确定按钮
    
    // 文本
    public Text ratingTitleTxt;              // 评分标题
    public Text lowStarTitleTxt;             // 低星标题
    public Text lowStarInfoTxt;              // 低星信息
    public Text thanksTitleTxt;              // 感谢标题
    public Text thanksInfoTxt;               // 感谢信息
    public Text submitBtnTxt;                // 提交按钮文本
    public Text okBtnTxt;                    // 确定按钮文本
    public Text placeholderTxt;              // 输入框占位符
    
    // 输入
    public InputField inputMsg;              // 反馈输入框
    
    // 图片
    public Image backGroundImg;              // 背景
    public Image rootImg;                    // 根节点背景
    // ... 更多图片组件
    
    /// <summary>
    /// 设置所有图标
    /// </summary>
    public void SetSprite(Sprite panelBg, Sprite panelContentBg, 
        Sprite iconStarOn, Sprite iconStarOff, ...)
}
```

---

## 使用指南

### 快速开始

#### 步骤 1: 准备资源

确保以下资源已添加到项目中（Addressables 或 Resources）：

```
rating/
└── guru_rating.prefab          # 评分弹窗预制体
    ├── 包含 UIRatingViewV1 组件
    
ui/rating/                      # 图标资源（可选自定义）
├── ic_close.png
├── ic_rating_star_on.png
├── ic_rating_star_off.png
├── ic_rating_thumb.png
├── ic_rating_heart.png
├── rating_panel_bg.png
└── rating_panel_content_bg.png
```

#### 步骤 2: 基础使用

```csharp
using Guru.SDK.UIUX.Popup;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform canvasTransform;
    
    public void ShowRating()
    {
        var controller = new RatingController();
        controller.Show(RatingSpec.Default(), OnRatingResult);
    }
    
    private void OnRatingResult(int stars, string feedback)
    {
        Debug.Log($"评分: {stars}, 反馈: {feedback}");
    }
}
```

#### 步骤 3: 高级使用（完整配置）

```csharp
using Guru.SDK.UIUX.Popup;
using UnityEngine;

public class RatingManager : MonoBehaviour
{
    [SerializeField] private Transform canvasTransform;
    
    private const string SUPPORT_EMAIL = "support@yourgame.com";
    
    public void ShowRating()
    {
        var spec = new RatingSpec
        {
            Container = canvasTransform,
            SupportEmail = SUPPORT_EMAIL,
            ShowImmediately = true,
            
            // 自定义文本（从多语言系统获取）
            TitleText = I2.Loc.LocalizationManager.GetTranslation("RATING_TITLE"),
            ThanksInfoText = I2.Loc.LocalizationManager.GetTranslation("RATING_THANKS"),
            
            // RTL 支持
            IsRTL = I2.Loc.LocalizationManager.IsRight2Left,
            
            // 自定义图标（可选）
            IconStarOn = "ui/rating/star_gold",
            IconStarOff = "ui/rating/star_gray",
        };
        
        var controller = new RatingController();
        controller.Show(spec, OnRatingComplete, OnRatingClosed);
    }
    
    private void OnRatingComplete(int stars, string message)
    {
        // 上报分析事件
        Analytics.LogEvent("user_rating", new Dictionary<string, object>
        {
            { "stars", stars },
            { "has_feedback", !string.IsNullOrEmpty(message) }
        });
        
        if (stars >= 4)
        {
            // 好评：引导到商店
            RequestStoreReview();
        }
        else
        {
            // 差评：记录反馈
            if (!string.IsNullOrEmpty(message))
            {
                SendFeedbackToServer(stars, message);
            }
        }
    }
    
    private void OnRatingClosed()
    {
        Debug.Log("评分弹窗关闭");
    }
    
    private void RequestStoreReview()
    {
        #if UNITY_IOS
        UnityEngine.iOS.Device.RequestStoreReview();
        #elif UNITY_ANDROID
        Application.OpenURL($"market://details?id={Application.identifier}");
        #endif
    }
    
    private void SendFeedbackToServer(int stars, string message)
    {
        // 发送到低星反馈服务器
        // ...
    }
}
```

---

## 配置说明

### 默认配置

```csharp
public static RatingSpec Default()
{
    return new RatingSpec
    {
        // 文本
        TitleText = "How would you rate us?",
        ThanksTitleText = "Thank you",
        ThanksInfoText = "Your support really means a lot.",
        LowStarTitleText = "Sorry to hear that",
        LowStarInfoText = "Please tell us what's wrong.\nIt will help us make the game better!",
        HighStarTitleText = "Thank you",
        HighStarInfoText = "Please leave your opinion.\nIt will help us make the game better!",
        PlaceholderText = "Write a comment",
        SubmitText = "Submit",
        OkText = "OK",
        
        // 行为
        AutoDestroy = true,
        IsRTL = false,
        ShowImmediately = false
    };
}
```

### 多语言配置示例

```csharp
public class LocalizedRatingConfig
{
    public static RatingSpec GetLocalizedSpec(Transform container)
    {
        var spec = new RatingSpec
        {
            Container = container,
            SupportEmail = "support@yourgame.com",
            ShowImmediately = true,
            IsRTL = IsRTLLanguage()
        };
        
        // 根据当前语言设置文本
        switch (GetCurrentLanguage())
        {
            case "zh-CN":
                spec.TitleText = "您喜欢我们的游戏吗？";
                spec.ThanksInfoText = "您的支持对我们意义重大。";
                spec.LowStarTitleText = "很抱歉听到这个消息";
                spec.LowStarInfoText = "请告诉我们哪里做得不好，\n这将帮助我们改进游戏！";
                spec.HighStarTitleText = "非常感谢";
                spec.HighStarInfoText = "请留下您的意见，\n这将帮助我们做得更好！";
                spec.PlaceholderText = "请输入您的反馈";
                spec.SubmitText = "提交";
                spec.OkText = "确定";
                break;
                
            case "ar": // 阿拉伯语
                spec.TitleText = "كيف تقيم تجربتك؟";
                spec.IsRTL = true; // 启用 RTL
                // ... 其他阿拉伯语文本
                break;
                
            case "ja": // 日语
                spec.TitleText = "ゲームの評価をお願いします";
                // ... 其他日语文本
                break;
                
            // 其他语言...
        }
        
        return spec;
    }
    
    private static bool IsRTLLanguage()
    {
        return I2.Loc.LocalizationManager.IsRight2Left;
    }
    
    private static string GetCurrentLanguage()
    {
        return I2.Loc.LocalizationManager.CurrentLanguage;
    }
}
```

---

## 事件流程

### 用户操作流程

```
┌─────────────────────────────────────────────────────────────────┐
│                     用户操作流程图                               │
└─────────────────────────────────────────────────────────────────┘

用户操作                    组件响应                      回调触发
─────────────────────────────────────────────────────────────────────
点击星星        ──────►   更新星星显示状态
   │                           │
   │                           ▼
   │                      判断星级
   │                      ├─ 5星 ──► 延迟显示感谢页 ──► 回调(5, "")
   │                      ├─ 4星 ──► 显示反馈输入页
   │                      └─ 1-3星 ─► 显示反馈输入页
   │                           │
   │                           ▼
   │                      用户输入反馈（可选）
   │                           │
   ▼                           ▼
点击提交        ──────►   显示感谢页 ───────────────► 回调(stars, message)
   │                           │
   │                           ▼
   │                      显示感谢信息
   │                      ├─ 5星：显示 ❤️
   │                      └─ 其他：显示 👍
   ▼                           │
点击确定/关闭   ──────►   关闭弹窗 ───────────────► onClosed 回调
```

### 回调时机

| 场景 | 触发回调 | 参数 |
|------|----------|------|
| 选择5星 | `onEvent` | `(5, "")` |
| 选择4星并提交 | `onEvent` | `(4, "用户输入的反馈")` |
| 选择1-3星并提交 | `onEvent` | `(stars, "用户输入的反馈")` |
| 关闭弹窗 | `onClosed` | 无 |

---

## 最佳实践

### 1. 评分触发时机

```csharp
public class RatingTrigger : MonoBehaviour
{
    // ✅ 推荐的触发时机
    
    // 1. 用户完成特定成就后
    public void OnLevelComplete(int level)
    {
        if (level == 5 || level == 10 || level == 20) // 关键节点
        {
            if (ShouldShowRating())
            {
                ShowRating();
            }
        }
    }
    
    // 2. 用户活跃一段时间后
    public void OnSessionCount(int sessionCount)
    {
        if (sessionCount == 3) // 第3次打开应用
        {
            ShowRating();
        }
    }
    
    // 3. 用户完成购买后
    public void OnPurchaseComplete()
    {
        // 付费用户更可能给好评
        ShowRating();
    }
    
    // ❌ 避免这些时机
    // - 应用启动时（用户还没体验）
    // - 游戏进行中（打断体验）
    // - 用户失败时（情绪不好）
    
    private bool ShouldShowRating()
    {
        // 检查是否已经评分过
        if (PlayerPrefs.HasKey("HasRated")) return false;
        
        // 检查冷却时间
        var lastShown = PlayerPrefs.GetInt("LastRatingShown", 0);
        var daysSinceLast = (DateTime.Now - new DateTime(lastShown)).Days;
        if (daysSinceLast < 7) return false; // 7天内不重复显示
        
        return true;
    }
}
```

### 2. 防止滥用

```csharp
public class RatingManager
{
    private const string PREFS_RATED = "user_has_rated";
    private const string PREFS_LAST_SHOWN = "rating_last_shown";
    private const string PREFS_SHOW_COUNT = "rating_show_count";
    private const int MAX_SHOW_COUNT = 3; // 最多显示3次
    private const int COOLDOWN_DAYS = 7;  // 冷却7天
    
    public bool CanShowRating()
    {
        // 1. 检查是否已评分
        if (PlayerPrefs.GetInt(PREFS_RATED, 0) == 1)
            return false;
        
        // 2. 检查显示次数
        var showCount = PlayerPrefs.GetInt(PREFS_SHOW_COUNT, 0);
        if (showCount >= MAX_SHOW_COUNT)
            return false;
        
        // 3. 检查冷却时间
        var lastShown = PlayerPrefs.GetString(PREFS_LAST_SHOWN, "");
        if (!string.IsNullOrEmpty(lastShown))
        {
            if (DateTime.TryParse(lastShown, out var lastDate))
            {
                if ((DateTime.Now - lastDate).TotalDays < COOLDOWN_DAYS)
                    return false;
            }
        }
        
        return true;
    }
    
    public void MarkAsRated()
    {
        PlayerPrefs.SetInt(PREFS_RATED, 1);
        PlayerPrefs.Save();
    }
    
    public void RecordShown()
    {
        var count = PlayerPrefs.GetInt(PREFS_SHOW_COUNT, 0);
        PlayerPrefs.SetInt(PREFS_SHOW_COUNT, count + 1);
        PlayerPrefs.SetString(PREFS_LAST_SHOWN, DateTime.Now.ToString("O"));
        PlayerPrefs.Save();
    }
}
```

### 3. 数据分析

```csharp
public class RatingAnalytics
{
    public void TrackRatingEvent(int stars, string feedback)
    {
        // 基础事件
        Analytics.LogEvent("in_app_rating", new Dictionary<string, object>
        {
            { "stars", stars },
            { "has_feedback", !string.IsNullOrEmpty(feedback) },
            { "feedback_length", feedback?.Length ?? 0 }
        });
        
        // 分级事件（便于漏斗分析）
        if (stars == 5)
        {
            Analytics.LogEvent("rating_5_star");
        }
        else if (stars >= 4)
        {
            Analytics.LogEvent("rating_positive", new { stars });
        }
        else
        {
            Analytics.LogEvent("rating_negative", new { stars });
        }
    }
}
```

---

## 常见问题

### Q1: 评分弹窗不显示？

**可能原因**:
1. `Container` 未设置或为 null
2. 预制体资源路径错误
3. Canvas 未激活

**排查方法**:
```csharp
public void ShowRating()
{
    if (canvasTransform == null)
    {
        Debug.LogError("Canvas transform is null!");
        return;
    }
    
    var spec = RatingSpec.Default();
    spec.Container = canvasTransform;
    
    // 检查资源是否存在
    var provider = GuruAssetManager.Instance;
    var view = provider.GetComponent<UIRatingViewV1>(RatingDefaults.RatingViewPath, spec.Container);
    if (view == null)
    {
        Debug.LogError($"Failed to load rating view from: {RatingDefaults.RatingViewPath}");
        return;
    }
    
    var controller = new RatingController();
    controller.Show(spec, OnRatingComplete);
}
```

### Q2: 如何自定义样式？

**方法1**: 修改 `RatingSpec` 配置
```csharp
var spec = new RatingSpec
{
    IconStarOn = "custom/star_gold",
    IconStarOff = "custom/star_gray",
    ImgPanelBg = "custom/panel_bg",
    // ...
};
```

**方法2**: 修改预制体
1. 找到 `rating/guru_rating` 预制体
2. 修改其中的 UI 元素
3. 保存并重新打包

### Q3: RTL 语言支持？

```csharp
// 自动检测并启用 RTL
var spec = new RatingSpec
{
    IsRTL = I2.Loc.LocalizationManager.IsRight2Left,
    // 或使用自定义检测
    // IsRTL = currentLanguage == "ar" || currentLanguage == "he"
};
```

### Q4: 5星时不显示商店？

检查 `ShowImmediately` 设置：
```csharp
var spec = new RatingSpec
{
    ShowImmediately = true,  // 5星时立即触发回调
    // 在回调中手动打开商店
};

controller.Show(spec, (stars, msg) => {
    if (stars == 5)
    {
        // iOS
        UnityEngine.iOS.Device.RequestStoreReview();
        // 或 Android
        Application.OpenURL("market://details?id=your.package");
    }
});
```

---

## 附录

### 完整代码示例

```csharp
using System;
using Guru.SDK.UIUX.Popup;
using UnityEngine;

namespace YourGame
{
    /// <summary>
    /// 评分管理器 - 完整示例
    /// </summary>
    public class GameRatingManager : MonoBehaviour
    {
        public static GameRatingManager Instance { get; private set; }
        
        [SerializeField] private Transform canvasTransform;
        [SerializeField] private string supportEmail = "support@yourgame.com";
        
        private const string PREFS_RATED = "user_rated";
        private const string PREFS_LAST_SHOW = "rating_last_show";
        private const int COOLDOWN_HOURS = 24;
        
        private void Awake()
        {
            Instance = this;
        }
        
        /// <summary>
        /// 尝试显示评分弹窗
        /// </summary>
        public void TryShowRating()
        {
            if (!CanShowRating())
            {
                Debug.Log("Rating cannot be shown at this time");
                return;
            }
            
            ShowRating();
        }
        
        private bool CanShowRating()
        {
            // 已评分
            if (PlayerPrefs.GetInt(PREFS_RATED, 0) == 1)
                return false;
            
            // 检查冷却
            var lastShowStr = PlayerPrefs.GetString(PREFS_LAST_SHOW, "");
            if (!string.IsNullOrEmpty(lastShowStr))
            {
                if (DateTime.TryParse(lastShowStr, out var lastShow))
                {
                    if ((DateTime.Now - lastShow).TotalHours < COOLDOWN_HOURS)
                        return false;
                }
            }
            
            return true;
        }
        
        private void ShowRating()
        {
            var spec = CreateSpec();
            var controller = new RatingController();
            
            controller.Show(spec, OnRatingComplete, OnRatingClosed);
            
            // 记录显示时间
            PlayerPrefs.SetString(PREFS_LAST_SHOW, DateTime.Now.ToString("O"));
            PlayerPrefs.Save();
        }
        
        private RatingSpec CreateSpec()
        {
            return new RatingSpec
            {
                Container = canvasTransform,
                SupportEmail = supportEmail,
                ShowImmediately = true,
                
                // 多语言文本
                TitleText = GetLocalizedText("RATING_TITLE"),
                ThanksInfoText = GetLocalizedText("RATING_THANKS"),
                LowStarTitleText = GetLocalizedText("RATING_LOW_TITLE"),
                LowStarInfoText = GetLocalizedText("RATING_LOW_INFO"),
                HighStarTitleText = GetLocalizedText("RATING_HIGH_TITLE"),
                HighStarInfoText = GetLocalizedText("RATING_HIGH_INFO"),
                PlaceholderText = GetLocalizedText("RATING_PLACEHOLDER"),
                SubmitText = GetLocalizedText("RATING_SUBMIT"),
                OkText = GetLocalizedText("RATING_OK"),
                
                // RTL 支持
                IsRTL = IsRTLLanguage()
            };
        }
        
        private void OnRatingComplete(int stars, string message)
        {
            Debug.Log($"[Rating] Stars: {stars}, Message: {message}");
            
            // 标记已评分
            if (stars >= 4)
            {
                PlayerPrefs.SetInt(PREFS_RATED, 1);
                PlayerPrefs.Save();
            }
            
            // 分析
            Analytics.LogEvent("in_app_rating", new
            {
                stars,
                has_feedback = !string.IsNullOrEmpty(message)
            });
            
            // 处理结果
            if (stars == 5)
            {
                OpenStoreReview();
            }
            else if (!string.IsNullOrEmpty(message))
            {
                // 低星有反馈，可以发送到服务器
                SendFeedback(stars, message);
            }
        }
        
        private void OnRatingClosed()
        {
            Debug.Log("[Rating] Popup closed");
        }
        
        private void OpenStoreReview()
        {
            #if UNITY_IOS
            UnityEngine.iOS.Device.RequestStoreReview();
            #elif UNITY_ANDROID
            Application.OpenURL($"market://details?id={Application.identifier}");
            #endif
        }
        
        private void SendFeedback(int stars, string message)
        {
            // 实现反馈发送逻辑
            Debug.Log($"[Feedback] {stars} stars: {message}");
        }
        
        private string GetLocalizedText(string key)
        {
            // 集成你的多语言系统
            #if I2_LOCALIZATION
            return I2.Loc.LocalizationManager.GetTranslation(key);
            #else
            return key;
            #endif
        }
        
        private bool IsRTLLanguage()
        {
            #if I2_LOCALIZATION
            return I2.Loc.LocalizationManager.IsRight2Left;
            #else
            return false;
            #endif
        }
    }
}
```

---

**文档版本**: 1.0  
**最后更新**: 2026-03-27  
**适用版本**: com.guru.sdk.uiux.popup 1.0+
