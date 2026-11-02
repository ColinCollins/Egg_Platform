using TMPro;
using UnityEngine;

public partial class CommonBtn : BaseAutoUIBind
{
    [SerializeField] private CustomButton btn;

    public CustomButton Btn => btn;

    public TextMeshProUGUI Content => ContentTxt;

    public override void Init() {}
}
