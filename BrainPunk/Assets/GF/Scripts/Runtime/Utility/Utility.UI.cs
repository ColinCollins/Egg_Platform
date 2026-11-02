using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GF
{
    public partial class Utility
    {
        public static class UI
        {
            public static void RefreshLayout(RectTransform rectTransform)
            {
                App.Instance.StartCoroutine(RefreshLayoutInner(rectTransform));
            }

            private static IEnumerator RefreshLayoutInner(RectTransform rectTransform)
            {
                yield return null;
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }
    }
}