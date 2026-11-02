using UnityEngine;

namespace GF
{
    public class KeepReverseWhenAdded: MonoBehaviour
    {
        private void Awake() {
            if (I2.Loc.LocalizationManager.IsRTL(I2.Loc.LocalizationManager.CurrentLanguageCode))
            {
                transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
            }
        }
    }
}