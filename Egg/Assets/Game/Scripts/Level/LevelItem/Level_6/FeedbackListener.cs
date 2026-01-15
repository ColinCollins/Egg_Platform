using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game.ItemEvent
{
    public class FeedbackListener : BaseItemEventHandle
    {
        [SerializeField] private MMF_Player fb;

        public override void Execute()
        {
            fb?.PlayFeedbacks();
            IsDone = true;
        }
    }

}
