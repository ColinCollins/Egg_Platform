using TMPro;
using UnityEngine;

/// <summary>135可乐爆开前的固定“?”和“!!!”气泡；复用原气泡美术，不查询或订阅多语言。</summary>
public sealed class Level135ColaAlert : MonoBehaviour
{
    #region 显式绑定
    /// <summary>本关气泡Prefab内的文字，按主控阶段显示固定问号或三个感叹号。</summary>
    [SerializeField] private TextMeshPro tmpTxt;
    /// <summary>原气泡弹出动画，仅用于本关实例的显示。</summary>
    [SerializeField] private Animator anim;
    /// <summary>相对喝到有气可乐的角色位置，沿用原气泡偏移。</summary>
    [SerializeField] private Vector3 followOffset = new Vector3(-0.4f, 1f, 0f);
    private Transform speaker;
    #endregion

    #region 本关显示与跟随
    /// <summary>主控进入Question阶段时显示固定问号；喷发或旧关清理时由主控销毁。</summary>
    public void ShowQuestion(Transform target)
    {
        speaker = target;
        tmpTxt.enableAutoSizing = true;
        tmpTxt.text = "?";
        tmpTxt.maxVisibleCharacters = int.MaxValue;
        transform.position = speaker.position + followOffset;
        anim.SetBool("show", true);
        anim.Update(0f);
    }

    /// <summary>问号停留结束后由主控切换为固定三个感叹号，沿用当前跟随对象，不走翻译。</summary>
    public void ShowExclamation()
    {
        tmpTxt.text = "!!!";
        tmpTxt.maxVisibleCharacters = int.MaxValue;
    }

    private void LateUpdate()
    {
        if (speaker != null) transform.position = speaker.position + followOffset;
    }
    #endregion
}
