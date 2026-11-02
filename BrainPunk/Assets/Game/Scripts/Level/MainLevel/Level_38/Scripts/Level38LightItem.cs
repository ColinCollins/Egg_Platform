using System.Collections;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 正确亮绿灯
/// 错误，播放红灯闪烁动画，0.5s 闪烁 3次。然后全部熄灭
/// </summary>
public class Level38LightItem : MonoBehaviour
{
    [SerializeField] private GameObject redLight;

    [SerializeField] private GameObject greenLight;

    private Coroutine _blinkCoroutine;

    private void Awake()
    {
        // 初始状态全部熄灭
        TurnOffAll();
    }

    /// <summary>
    /// 显示绿灯（正确时）
    /// </summary>
    public void ShowGreenLight()
    {
        StopBlink();
        if (redLight != null)
            redLight.SetActive(false);
        if (greenLight != null)
            greenLight.SetActive(true);

        AudioManager.PlaySound("passwordCorrect");
    }

    /// <summary>
    /// 显示红灯，常量
    /// </summary>
    public void ShowRedLight()
    {
        StopBlink();
        if (redLight != null)
            redLight.SetActive(true);
        if (greenLight != null)
            greenLight.SetActive(false);
    }

    /// <summary>
    /// 播放红灯闪烁动画（错误时）
    /// 0.5s 闪烁 3次，然后全部熄灭
    /// </summary>
    public void PlayRedBlink()
    {
        StopBlink();
        _blinkCoroutine = StartCoroutine(BlinkRedLight());

        AudioManager.PlaySound("passwordError");
    }

    /// <summary>
    /// 全部熄灭
    /// </summary>
    public void TurnOffAll()
    {
        StopBlink();
        if (redLight != null)
            redLight.SetActive(false);
        if (greenLight != null)
            greenLight.SetActive(false);
    }

    private void StopBlink()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
    }

    private IEnumerator BlinkRedLight()
    {
        if (redLight == null)
            yield break;

        // 确保绿灯关闭
        if (greenLight != null)
            greenLight.SetActive(false);

        // 0.5s 闪烁 3次 = 每次闪烁约 0.167s (0.5s / 3)
        // 一个完整闪烁周期：亮 -> 灭，各占一半时间
        float blinkInterval = 0.5f / 6f; // 0.083s 每次状态切换

        for (int i = 0; i < 3; i++)
        {
            // 亮
            redLight.SetActive(true);
            yield return new WaitForSeconds(blinkInterval);
            // 灭
            redLight.SetActive(false);
            yield return new WaitForSeconds(blinkInterval);
        }

        // 闪烁结束后全部熄灭
        TurnOffAll();
    }
}
