using Config;
using UnityEngine;

public class LanguageItem : MonoBehaviour
{
    [SerializeField] private CommonBtn normalBtn;
    [SerializeField] private CommonBtn selectedBtn;

    private Language _data;
    private string languageName;
    private string languageKeyCode;
    private System.Action<LanguageItem> _onClick;

    public void Init(Language data, bool isSelected, System.Action<LanguageItem> onClick)
    {
        _data = data;
        languageName = data.ContyrName;
        languageKeyCode = data.CorrespondingKey;
        _onClick = onClick;

        if (normalBtn != null && normalBtn.Content != null)
        {
            normalBtn.Content.text = languageName;
        }

        if (selectedBtn != null && selectedBtn.Content != null)
        {
            selectedBtn.Content.text = languageName;
        }

        SetSelected(isSelected);

        if (normalBtn != null && normalBtn.Btn != null)
        {
            normalBtn.Btn.OnClick += OnClickButton;
        }

        if (selectedBtn != null && selectedBtn.Btn != null)
        {
            selectedBtn.Btn.OnClick += OnClickButton;
        }
    }

    public void InitCustom(bool isSelected, System.Action<LanguageItem> onClick)
    {
        _data = null;
        languageKeyCode = string.Empty;
        _onClick = onClick;

        SetSelected(isSelected);

        if (normalBtn != null && normalBtn.Btn != null)
        {
            normalBtn.Btn.OnClick += OnClickButton;
        }

        if (selectedBtn != null && selectedBtn.Btn != null)
        {
            selectedBtn.Btn.OnClick += OnClickButton;
        }
    }

    public void SetSelected(bool selected)
    {
        if (normalBtn != null)
        {
            normalBtn.gameObject.SetActive(!selected);
        }

        if (selectedBtn != null)
        {
            selectedBtn.gameObject.SetActive(selected);
        }
    }

    public string GetLanguageKeyCode() => languageKeyCode;

    private void OnClickButton(CustomButton btn)
    {
        _onClick?.Invoke(this);
    }
}
