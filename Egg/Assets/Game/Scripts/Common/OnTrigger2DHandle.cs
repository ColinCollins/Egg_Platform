using System;
using UnityEngine;

public class OnTrigger2DHandle : MonoBehaviour
{
    public Action<Collider2D> OnEnter;

    private void OnTriggerEnter2D(Collider2D other) {
        OnEnter?.Invoke(other);
    }
}
