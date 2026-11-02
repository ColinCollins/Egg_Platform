using UnityEngine;

// [ExecuteInEditMode]
public class Level31OnParticalTrigger : MonoBehaviour
{
    [SerializeField] private Level31Ctrl levelCtrl;

    private void OnParticleCollision(GameObject other)
    {
        Debug.Log("Trigger Particle");
        // levelCtrl.CutdownScore();
    }

}
