using UnityEngine;

public class FollowTargetRotate : MonoBehaviour
{
    public Transform Target;

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Target == null)
            return;
            
        transform.localEulerAngles = Target.localEulerAngles;
    }
}
