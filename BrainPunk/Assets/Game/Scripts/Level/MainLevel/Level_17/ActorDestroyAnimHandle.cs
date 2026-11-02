using DG.Tweening;
using UnityEngine;

public class ActorDestroyAnimHandle : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform endTarget;
    [SerializeField] private float duration;

    [SerializeField] private Vector3 enterDoorOffset = new Vector3(0, 1, 0);

    [SerializeField] private bool hasSpine;

    private Sequence seq;
    private ActorSpineCtrl spine;

    public void SetTarget(Transform trans)
    {
        target = trans;
    }

    public void PlayDestroy()
    {
        float alpha = 1f;
        if (hasSpine)
            spine = target.GetComponent<ActorSpineCtrl>();

        if (seq == null)
            seq = DOTween.Sequence();
        else
        {
            return;
        }
        seq.Append(target.DOMove(endTarget.position + enterDoorOffset, duration));
        seq.Join(target.DOScale(Vector3.zero, duration));
        if (hasSpine)
            seq.Join(DOTween.To(() => alpha, x =>
            {
                if (spine)
                    spine.SetAlpha(x);
            }, 0, duration));

        var angle = target.localEulerAngles;
        angle.z = angle.y != 0 ? -720 : 720;
        seq.Join(target.DORotate(angle, duration, RotateMode.FastBeyond360));
        seq.OnComplete(() =>
        {
            if (target != null && target.gameObject)
                Destroy(target.gameObject);
        });
    }

    void OnDestroy()
    {
        if (seq != null)
            seq.Kill();
    }
}
