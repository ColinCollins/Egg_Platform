using UnityEngine;

public class Level5Anchor : MonoBehaviour
{
    private SpringJoint2D springJoint;
    [SerializeField] private Level5TrackLine trackLine;
    private bool isCut = false;

    public SpringJoint2D SpringJoint => springJoint;
    public Level5TrackLine TrackLine => trackLine;
    public bool IsCut => isCut;

    void Awake()
    {
        springJoint = GetComponent<SpringJoint2D>();
    }

    public void SetCut(bool cut)
    {
        isCut = cut;
    }
}
