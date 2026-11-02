using MoreMountains.Feedbacks;
using UnityEngine;

public class TrafficLightCollisionListener : MonoBehaviour
{
    [SerializeField] private TrafficLightCtrl ctrl;
    [SerializeField] MMF_Player feedback;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            feedback?.PlayFeedbacks();

            Destroy(GetComponent<Rigidbody2D>());
            Destroy(GetComponent<BoxCollider2D>());

            Destroy(GetComponentInParent<FollowTarget>());

            ctrl.Broken();
        }
    }
}
