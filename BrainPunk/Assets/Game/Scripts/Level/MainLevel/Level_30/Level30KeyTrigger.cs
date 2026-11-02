using UnityEngine;

public class Level30WingTrigger : MonoBehaviour
{
    [SerializeField] private Level30Ctrl levelCtrl;
    void OnTriggerEnter2D(Collider2D collision)
    {
        var follower = GetComponent<FollowTarget>();
        if (follower != null)
        {
            levelCtrl.OnWingStartDrag(follower.Target);
            levelCtrl.OnWingEndDrag(follower.Target);
        }     
    }
}
