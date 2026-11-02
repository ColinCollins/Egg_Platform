using UnityEngine;

public class FollowTargetScale : MonoBehaviour
{
    public Transform Target;

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Target == null)
            return;
        transform.localScale = Target.localScale;
    }
}
