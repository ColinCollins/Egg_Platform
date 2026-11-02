using Cysharp.Threading.Tasks;
using DG.Tweening;
using MelenitasDev.SoundsGood;
using UnityEngine;

namespace Game.Turntable
{
    public class TurntableItemCtrl : MonoBehaviour
    {
        [SerializeField] private GameObject key;
        [SerializeField] private GameObject keyStartPoint;
        [SerializeField] private Transform turntable;
        [SerializeField] private ThrowItemHandle throwHandle;

        // [SerializeField] private TriggerAreaHandle triggerKey;

        [SerializeField] private TurntableButton button;


        [Header("补充")]
        [SerializeField] private GameObject otherStuff;

        private bool isKey = false;
        private bool isOther = false;
        private bool isPlaying = false;
        private bool isFinished = false;
        private Sound _turntableSound;

        void Start()
        {
            button.SetEnter(Play);
        }

        public void Play()
        {
            if (isPlaying || isFinished)
                return;

            isPlaying = true;
            _turntableSound = Game.Scripts.Common.AudioManager.PlaySound("turnntableStart", loop: true);
            turntable.DORotate(new Vector3(0, 0, 360 * 20f), 3f, RotateMode.FastBeyond360).SetEase(Ease.InOutCirc).OnComplete(() =>
            {
                if (_turntableSound != null && _turntableSound.Using)
                    _turntableSound.Stop(0f);
                _turntableSound = null;
                ReadyToThrow().Forget();
            });
        }

        public void EnterArea()
        {
            isKey = true;
        }

        public void ExitArea()
        {
            isKey = false;
        }

        public void EnterOtherArea()
        {
            isOther = true;
        }

        public void ExitOtherArea()
        {
            isOther = false;
        }


        async UniTask ReadyToThrow()
        {
            await UniTask.WaitForSeconds(0.1f);
            Game.Scripts.Common.AudioManager.PlaySound("turntableGetReward");
            isPlaying = false;

            if (isKey && key != null)
            {
                keyStartPoint.SetActive(false);
                key.SetActive(true);
                isFinished = true;
            }
            else if (isOther && otherStuff != null && !otherStuff.activeSelf)
            {
                otherStuff.SetActive(true);
                otherStuff = null;
                // isFinished = true;
            }
            else
            {
                throwHandle.SpawnItem();
            }
        }

        void OnDestroy()
        {
            if (turntable)
            {
                turntable.DOKill();
            }
        }
    }
}