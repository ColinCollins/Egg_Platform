using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using UnityEngine;
using System.Collections.Generic;

public class Level7Ctrl : MonoBehaviour
{
    private struct RigidbodySnapshot
    {
        public RigidbodyType2D BodyType;
        public bool Simulated;
        public bool UseFullKinematicContacts;
        public bool UseAutoMass;
        public float Mass;
        public float LinearDamping;
        public float AngularDamping;
        public float GravityScale;
        public PhysicsMaterial2D SharedMaterial;
        public RigidbodyInterpolation2D Interpolation;
        public RigidbodySleepMode2D SleepMode;
        public CollisionDetectionMode2D CollisionDetectionMode;
        public RigidbodyConstraints2D Constraints;
    }

    public MMF_Player camerShake;

    public Rigidbody2D boxRigibody;

    [SerializeField] private float heightLimit = 2f;
    [Header("Drag Collision")]
    [SerializeField] private bool ignoreCollisionOnDrag = true;
    [SerializeField] private LayerMask ignoredLayersOnDrag;

    private Transform boxTransform;

    private bool canShakeCamera = false;

    private bool canPlaySound = true;
    private bool isInGround = true;
    private bool isIgnoringDragCollision;
    private bool hasBoxRigidbodySnapshot;
    private Collider2D[] boxColliders;
    private readonly List<Collider2D> ignoredCollidersOnDrag = new();
    private RigidbodySnapshot boxRigidbodySnapshot;

    void Start()
    {
        boxTransform = boxRigibody.transform;
        boxColliders = boxRigibody.GetComponentsInChildren<Collider2D>();
        CacheBoxRigidbodySnapshot();

        if (ignoredLayersOnDrag.value == 0)
        {
            ignoredLayersOnDrag = LayerMask.GetMask("Ground");
        }

        Time.fixedDeltaTime = 1 / 120f;
    }

    private void OnDisable()
    {
        EnsureBoxRigidbody();
        SetDragCollisionIgnored(false);
    }

    public void EnterGround()
    {
        if (isInGround || camerShake.IsPlaying || !canShakeCamera)
            return;

        isInGround = true;
        canShakeCamera = false;
        camerShake.PlayFeedbacks();


        if (canPlaySound)
        {
            canPlaySound = false;
            AudioManager.PlaySound("crash");
        }

    }

    public void ExitLayer()
    {
        isInGround = false;
    }

    public void StartDrag()
    {
        if (boxRigibody == null)
            return;

        CacheBoxRigidbodySnapshot();
        SetDragCollisionIgnored(true);
        DestroyImmediate(boxRigibody);
        boxRigibody = null;
    }

    public void EndDrag()
    {
        EnsureBoxRigidbody();
        SetDragCollisionIgnored(false);

        if (boxRigibody != null && boxRigibody.transform.position.y > -.1f)
        {
            canPlaySound = true;
        }

        if (boxTransform == null)
        {
            return;
        }

        canShakeCamera = boxTransform.position.y > heightLimit;
    }

    private void SetDragCollisionIgnored(bool ignore)
    {
        if (boxColliders == null || isIgnoringDragCollision == ignore)
            return;

        if (ignore && !ignoreCollisionOnDrag)
            return;

        isIgnoringDragCollision = ignore;

        if (ignore)
        {
            ignoredCollidersOnDrag.Clear();
            Collider2D[] colliders = FindObjectsOfType<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D targetCollider = colliders[i];
                if (targetCollider == null || !ShouldIgnoreOnDrag(targetCollider))
                    continue;

                ignoredCollidersOnDrag.Add(targetCollider);
                SetIgnoreCollisionWithBox(targetCollider, true);
            }

            return;
        }

        for (int i = 0; i < ignoredCollidersOnDrag.Count; i++)
        {
            Collider2D targetCollider = ignoredCollidersOnDrag[i];
            if (targetCollider == null)
                continue;

            SetIgnoreCollisionWithBox(targetCollider, false);
        }

        ignoredCollidersOnDrag.Clear();
    }

    private bool ShouldIgnoreOnDrag(Collider2D targetCollider)
    {
        if (targetCollider == null)
            return false;

        if (((1 << targetCollider.gameObject.layer) & ignoredLayersOnDrag.value) == 0)
            return false;

        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (boxColliders[i] == targetCollider)
                return false;
        }

        return true;
    }

    private void SetIgnoreCollisionWithBox(Collider2D targetCollider, bool ignore)
    {
        if (targetCollider == null || boxColliders == null)
            return;

        for (int i = 0; i < boxColliders.Length; i++)
        {
            Collider2D boxCollider = boxColliders[i];
            if (boxCollider == null)
                continue;

            Physics2D.IgnoreCollision(boxCollider, targetCollider, ignore);
        }
    }

    private void CacheBoxRigidbodySnapshot()
    {
        if (boxRigibody == null)
            return;

        boxRigidbodySnapshot = new RigidbodySnapshot
        {
            BodyType = boxRigibody.bodyType,
            Simulated = boxRigibody.simulated,
            UseFullKinematicContacts = boxRigibody.useFullKinematicContacts,
            UseAutoMass = boxRigibody.useAutoMass,
            Mass = boxRigibody.mass,
            LinearDamping = boxRigibody.linearDamping,
            AngularDamping = boxRigibody.angularDamping,
            GravityScale = boxRigibody.gravityScale,
            SharedMaterial = boxRigibody.sharedMaterial,
            Interpolation = boxRigibody.interpolation,
            SleepMode = boxRigibody.sleepMode,
            CollisionDetectionMode = boxRigibody.collisionDetectionMode,
            Constraints = boxRigibody.constraints
        };
        hasBoxRigidbodySnapshot = true;
    }

    private void EnsureBoxRigidbody()
    {
        if (boxTransform == null)
            return;

        if (boxRigibody == null)
        {
            boxRigibody = boxTransform.GetComponent<Rigidbody2D>();
        }

        if (boxRigibody == null)
        {
            boxRigibody = boxTransform.gameObject.AddComponent<Rigidbody2D>();
        }

        if (!hasBoxRigidbodySnapshot)
            return;

        boxRigibody.bodyType = boxRigidbodySnapshot.BodyType;
        boxRigibody.useFullKinematicContacts = boxRigidbodySnapshot.UseFullKinematicContacts;
        boxRigibody.sharedMaterial = boxRigidbodySnapshot.SharedMaterial;
        boxRigibody.linearDamping = boxRigidbodySnapshot.LinearDamping;
        boxRigibody.angularDamping = boxRigidbodySnapshot.AngularDamping;
        boxRigibody.gravityScale = boxRigidbodySnapshot.GravityScale;
        boxRigibody.interpolation = boxRigidbodySnapshot.Interpolation;
        boxRigibody.sleepMode = boxRigidbodySnapshot.SleepMode;
        boxRigibody.collisionDetectionMode = boxRigidbodySnapshot.CollisionDetectionMode;
        boxRigibody.constraints = boxRigidbodySnapshot.Constraints;
        boxRigibody.simulated = boxRigidbodySnapshot.Simulated;
        boxRigibody.useAutoMass = boxRigidbodySnapshot.UseAutoMass;

        if (!boxRigidbodySnapshot.UseAutoMass)
        {
            boxRigibody.mass = boxRigidbodySnapshot.Mass;
        }
    }
}
