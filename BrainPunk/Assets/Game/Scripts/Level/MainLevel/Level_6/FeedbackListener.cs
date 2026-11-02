using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game.ItemEvent
{
    public class FeedbackListener : BaseItemEventHandle
    {
        [SerializeField] private MMF_Player fb;
        [SerializeField] private bool isWaitingFinished;

        public override void Execute()
        {
            if (fb.IsPlaying)
                return;

            fb?.PlayFeedbacks();
            if (isWaitingFinished)
            {
                fb.Events.OnComplete.AddListener(() =>
                            {
                                IsDone = true;
                            });
            }
            else
            {
                IsDone = true;
            }



        }
    }

}
