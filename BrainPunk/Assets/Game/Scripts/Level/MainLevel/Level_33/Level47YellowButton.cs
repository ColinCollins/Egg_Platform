using UnityEngine;

namespace Game.Turntable
{
    public class Level47YellowButton : PressButtonBase
    {
        private Vector3 _buttonOriginLocalPos;

        protected override void Start()
        {
            _buttonOriginLocalPos = transform.localPosition;
            base.Start();
        }

        protected override void Update()
        {
            _buttonOriginPos = transform.parent != null
                ? transform.parent.TransformPoint(_buttonOriginLocalPos)
                : _buttonOriginLocalPos;

            base.Update();
            ApplyState(GetCurrentState01());
        }
    }
}
