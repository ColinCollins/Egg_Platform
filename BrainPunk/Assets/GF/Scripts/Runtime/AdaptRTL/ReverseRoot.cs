using System;
using I2.Loc;
using UnityEngine;
using UnityEngine.UI;

namespace GF
{
    public class ReverseRoot: MonoBehaviour
    {
        private Vector3 _originScale;
        private bool _alreadyReverse = false;

        public bool isSubRoot = false;

        private void Awake()
        {
            _originScale = transform.localScale;
            if (LocalizationManager.IsRight2Left)
            {
                Reverse();
            }
        }

        public void Reverse()
        {
            if (_alreadyReverse)
            {
                return;
            }
            _alreadyReverse = true;
            if (!isSubRoot)
            {
                var curScale = _originScale;
                curScale.x = -curScale.x;
                transform.localScale = curScale;
            }
            
            RefreshChildText();
            RefreshChildKeep(true);

        }

        public void Revert()
        {
            if (!_alreadyReverse)
            {
                return;
            }
            _alreadyReverse = false;
            transform.localScale = _originScale;
            RefreshChildKeep(false);
            RefreshChildText();
        }
        
        private void RefreshChildKeep(bool reverse)
        {
            KeepReverse[] keeps = GetComponentsInChildren<KeepReverse>(true);
            for (int i = 0; i < keeps.Length; i++)
            {
                KeepReverse keep = keeps[i];
                
                var scale = keep.transform.localScale;
                if ((reverse && scale.x > 0)||(!reverse && scale.x<0))
                {
                    scale.x = -scale.x;
                }
                
                keep.transform.localScale = scale;
            }
        }

        private void RefreshChildText()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text.GetComponentInParent<KeepReverse>(true) != null)
                {
                    continue;
                }
                
                if (text.alignment == TextAnchor.LowerLeft || text.alignment == TextAnchor.MiddleLeft ||
                    text.alignment == TextAnchor.UpperLeft)
                {
                    text.alignment = text.alignment + 2;
                }
                else if (text.alignment == TextAnchor.LowerRight || text.alignment == TextAnchor.MiddleRight ||
                         text.alignment == TextAnchor.UpperRight)
                {
                    text.alignment = text.alignment - 2;
                }

                var scale = text.transform.localScale;
                scale.x = -scale.x;
                text.transform.localScale = scale;
            }
        }
    }
}