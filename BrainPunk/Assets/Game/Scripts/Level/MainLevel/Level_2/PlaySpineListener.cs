using UnityEngine;

namespace Game.ItemEvent
{
    public class PlaySpineListener : BaseItemEventHandle
    {
        [SerializeField] private ActorSpineCtrl target;
        [SerializeField] private string spineAnimName;
        [SerializeField] private bool isLoop;

        [SerializeField] private bool isWaiting;

        private Spine.TrackEntry entry;

        public override void Execute()
        {
            IsRunning = true;
            entry = target.PlayAnimation(spineAnimName, isLoop);
            if (isLoop)
            {
                IsDone = true;
            }
            else
            {
                if (isWaiting)
                    entry.Complete += (entry) =>
                    {
                        IsRunning = false;
                        IsDone = true;
                    };
                else
                {
                    IsRunning = false;
                    IsDone = true;
                }
            }
        }
    }

}
