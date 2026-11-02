using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace GF
{
    public static class DOTweenExtension
    {
        /// <summary>
        /// 在RTL下，使用DOScale等方法时，没有考虑到RTL的问题，可以使用这个方法来修复
        /// </summary>
        /// <param name="tweener"></param>
        /// <returns></returns>
        public static Tweener FixRTL(this Tweener tweener)
        {
            if (I2.Loc.LocalizationManager.IsRTL(I2.Loc.LocalizationManager.CurrentLanguageCode))
            {
                if(tweener is TweenerCore<Vector3, Vector3, VectorOptions> floatTweener)
                {
                    floatTweener.ChangeEndValue(
                        new Vector3(-floatTweener.endValue.x, floatTweener.endValue.y, floatTweener.endValue.z), false);
                }
            }

            return tweener;
        }
    }
}