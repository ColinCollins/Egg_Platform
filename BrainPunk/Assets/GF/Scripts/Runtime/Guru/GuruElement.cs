using UnityEngine;

namespace GF.Guru
{
    public abstract class GuruElement : MonoBehaviour
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            OnInitialized();
        }

        protected virtual void OnInitialized()
        {
        }
    }
}