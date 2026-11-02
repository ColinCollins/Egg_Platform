using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

public class Level23Ctrl : MonoBehaviour
{
    [SerializeField] private BaseLevelCtrl levelCtrl;
    
    private bool isSuccess = false;
    // 通关
    public void ClickLevelSuccess()
    {
        if (isSuccess)
            return;

        isSuccess = true;
        levelCtrl.SuccessStrightly();
    }
}
