using Game.Scripts.Common;
using UnityEngine;

public class Level15Ctrl : MonoBehaviour
{
    [SerializeField] private ParticleSystem knock;
    [SerializeField] private Transform light;

    public void PlayKnock(Transform currentClicker, Vector2 worldPoint)
    {
        var obj = Instantiate(knock, light);
        obj.gameObject.SetActive(true);
        obj.transform.position = worldPoint;

        AudioManager.PlaySound("trap");
    }
}
