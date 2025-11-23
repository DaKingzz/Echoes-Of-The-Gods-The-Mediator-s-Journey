using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyMeleeAttack
/// - Trigger-driven melee component that decides attacks itself (no external calls).
/// - Starts windup when a player enters the attackArea trigger; animation must call OnAttackHitFrame and OnAttackEndFrame.
/// - Uses a cached animator hash (IsAttacking) and robust trigger handling that finds IPlayer on children/parents.
/// - Mirrors the attackArea automatically when the visual flips (detects facing via SpriteRenderer.flipX or transform.localScale.x).
/// - Optionally repeats attacks while the player remains inside and optionally cancels windup if all leave.
/// </summary>
[DisallowMultipleComponent]
public class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Timing")] [Tooltip("Windup time from player entering until animation trigger.")] [SerializeField]
    private float windupDelay = 0.35f;

    [Tooltip("Minimum time between completed attacks.")] [SerializeField]
    private float attackCooldown = 1.0f;

    [Header("Damage")] [Tooltip("Damage applied to the player when hit.")] [SerializeField]
    private float damage = 5f;

    [Header("Attack Area (trigger)")]
    [Tooltip("Trigger collider that represents reach of the attack. Should be IsTrigger = true and enabled.")]
    [SerializeField]
    private Collider2D attackArea;

    [Header("Animator")]
    [Tooltip(
        "Animator used to play the attack animation. If null the component will try to find one on self/children.")]
    [SerializeField]
    private Animator animator;

    // cached animator hash (no inspector field)
    private static readonly int IsAttacking = Animator.StringToHash("isAttacking");

    [Header("Behavior")]
    [Tooltip("If true, windup will cancel if no players remain in the area during windup.")]
    [SerializeField]
    private bool cancelIfEmptyDuringWindup = true;

    [Tooltip(
        "If true, automatically attempt another attack after cooldown while a player is still inside the attackArea.")]
    [SerializeField]
    private bool repeatWhileInside = true;

    [Header("Facing / Mirroring")]
    [Tooltip(
        "Optional SpriteRenderer to detect facing via flipX. If null the script falls back to checking transform.localScale.x.")]
    [SerializeField]
    private SpriteRenderer spriteRendererForFacing;

    [Tooltip("If your art faces right by default set true; if it faces left by default set false.")] [SerializeField]
    private bool artFacesRightByDefault = true;

    [Header("Debug")]
    [Tooltip("When enabled, logs trigger hits and facing/mirroring changes (editor-only).")]
    [SerializeField]
    private bool debugLogs = false;

    private const string playerTag = "Player";

    // runtime state
    // track actual colliders overlapping the attackArea for robust enter/exit bookkeeping
    private readonly HashSet<Collider2D> overlappingColliders = new HashSet<Collider2D>();
    private volatile bool isWindingUp = false;
    private float lastAttackTime = -Mathf.Infinity;
    private bool awaitingNextAttack = false;

    // Facing tracking
    private bool lastFacingRight = true;

    // Cache original offsets/positions for symmetric mirroring
    private float originalColliderOffsetX = float.NaN; // used if collider is on same transform
    private float originalChildLocalPosX = float.NaN; // used if attackArea is on a child transform
    private bool attackAreaIsChild = false;

    private void Reset()
    {
        if (attackArea == null) attackArea = GetComponentInChildren<Collider2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRendererForFacing == null) spriteRendererForFacing = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRendererForFacing == null) spriteRendererForFacing = GetComponentInChildren<SpriteRenderer>();

        // Determine how attackArea is attached and cache original offsets/positions
        if (attackArea != null)
        {
            attackAreaIsChild = attackArea.transform != this.transform;
            if (attackAreaIsChild)
            {
                originalChildLocalPosX = attackArea.transform.localPosition.x;
            }
            else
            {
                if (attackArea is BoxCollider2D box)
                    originalColliderOffsetX = box.offset.x;
                else if (attackArea is CircleCollider2D circ)
                    originalColliderOffsetX = circ.offset.x;
                else
                    originalColliderOffsetX = attackArea.offset.x;
            }
        }

        // Initialize facing and ensure collider is positioned accordingly
        lastFacingRight = GetCurrentFacingRight();
        ApplyMirrorForFacing(lastFacingRight);
    }

    private void Update()
    {
        bool facingRight = GetCurrentFacingRight();
        if (facingRight != lastFacingRight)
        {
            lastFacingRight = facingRight;
            ApplyMirrorForFacing(facingRight);
            if (debugLogs)
                Debug.Log($"[EnemyMeleeAttack] Facing changed. Now facingRight={facingRight}. Applied mirror.", this);
        }
    }

    private bool GetCurrentFacingRight()
    {
        if (spriteRendererForFacing != null)
        {
            bool spriteShowsRight = !spriteRendererForFacing.flipX;
            return artFacesRightByDefault ? spriteShowsRight : !spriteShowsRight;
        }
        else
        {
            bool scalePositive = transform.localScale.x >= 0f;
            return artFacesRightByDefault ? scalePositive : !scalePositive;
        }
    }

    private void ApplyMirrorForFacing(bool facingRight)
    {
        if (attackArea == null) return;
        float sign = facingRight ? +1f : -1f;

        if (attackAreaIsChild)
        {
            Vector3 lp = attackArea.transform.localPosition;
            float absX = float.IsNaN(originalChildLocalPosX) ? Mathf.Abs(lp.x) : Mathf.Abs(originalChildLocalPosX);
            lp.x = absX * sign;
            attackArea.transform.localPosition = lp;
        }
        else
        {
            if (attackArea is BoxCollider2D box)
            {
                Vector2 off = box.offset;
                float abs = float.IsNaN(originalColliderOffsetX)
                    ? Mathf.Abs(off.x)
                    : Mathf.Abs(originalColliderOffsetX);
                off.x = abs * sign;
                box.offset = off;
            }
            else if (attackArea is CircleCollider2D circ)
            {
                Vector2 off = circ.offset;
                float abs = float.IsNaN(originalColliderOffsetX)
                    ? Mathf.Abs(off.x)
                    : Mathf.Abs(originalColliderOffsetX);
                off.x = abs * sign;
                circ.offset = off;
            }
            else
            {
                Vector2 off = attackArea.offset;
                float abs = float.IsNaN(originalColliderOffsetX)
                    ? Mathf.Abs(off.x)
                    : Mathf.Abs(originalColliderOffsetX);
                off.x = abs * sign;
                attackArea.offset = off;
            }
        }
    }

    // helper: returns true if this collider belongs to a player (has IPlayer in parents or parent tagged Player with IPlayer child)
    private bool ColliderIsPlayer(Collider2D col)
    {
        if (col == null) return false;
        if (col.GetComponentInParent<IPlayer>() != null) return true;

        Transform t = col.transform;
        while (t != null)
        {
            if (t.CompareTag(playerTag))
            {
                if (t.GetComponentInChildren<IPlayer>() != null) return true;
            }

            t = t.parent;
        }

        return false;
    }

    private bool AnyPlayerPresent()
    {
        lock (overlappingColliders)
        {
            foreach (var c in overlappingColliders)
                if (ColliderIsPlayer(c))
                    return true;
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // only track player colliders
        if (!ColliderIsPlayer(other))
        {
            if (debugLogs) Debug.Log($"[EnemyMeleeAttack] Ignored non-player collider '{other.name}' on Enter.", this);
            return;
        }

        lock (overlappingColliders)
        {
            overlappingColliders.Add(other);
        }

        if (debugLogs)
            Debug.Log(
                $"[EnemyMeleeAttack] Player collider Enter '{other.name}' added. Count={overlappingColliders.Count}.",
                this);

        // Start windup if not already winding and cooldown passed
        if (!isWindingUp && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(WindupCoroutine());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        // ignore non-player colliders consistently
        if (!ColliderIsPlayer(other))
        {
            if (debugLogs) Debug.Log($"[EnemyMeleeAttack] Ignored non-player collider '{other.name}' on Exit.", this);
            return;
        }

        lock (overlappingColliders)
        {
            overlappingColliders.Remove(other);
        }

        if (debugLogs)
            Debug.Log(
                $"[EnemyMeleeAttack] Player collider Exit '{other.name}' removed. Count={overlappingColliders.Count}.",
                this);
    }

    private IEnumerator WindupCoroutine()
    {
        isWindingUp = true;

        float start = Time.time;
        while (Time.time - start < windupDelay)
        {
            if (cancelIfEmptyDuringWindup)
            {
                bool anyPlayer;
                lock (overlappingColliders)
                {
                    anyPlayer = false;
                    foreach (var col in overlappingColliders)
                    {
                        if (ColliderIsPlayer(col))
                        {
                            anyPlayer = true;
                            break;
                        }
                    }
                }

                if (!anyPlayer)
                {
                    isWindingUp = false;
                    if (debugLogs) Debug.Log("[EnemyMeleeAttack] Windup cancelled because player(s) left.", this);
                    yield break;
                }
            }

            yield return null;
        }

        if (animator != null)
            animator.SetTrigger(IsAttacking);
    }

    /// <summary>
    /// Animation event: called on the frame where the weapon is extended.
    /// Applies damage to any IPlayer currently tracked as inside the attackArea.
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (!isWindingUp) return;

        // snapshot current colliders to avoid locking while calling player code
        Collider2D[] snapshot;
        lock (overlappingColliders)
        {
            snapshot = new Collider2D[overlappingColliders.Count];
            overlappingColliders.CopyTo(snapshot);
        }

        // map colliders -> IPlayer, avoid duplicates when player has multiple colliders
        var hitPlayers = new HashSet<IPlayer>();
        for (int i = 0; i < snapshot.Length; i++)
        {
            var col = snapshot[i];
            if (col == null) continue;
            var p = col.GetComponentInParent<IPlayer>();
            if (p != null) hitPlayers.Add(p);
        }

        if (debugLogs)
            Debug.Log($"[EnemyMeleeAttack] OnAttackHitFrame applying damage to {hitPlayers.Count} players.", this);

        foreach (var player in hitPlayers)
        {
            player?.TakeDamage(damage);
        }
    }

    /// <summary>
    /// Animation event: called at the end of the attack animation to close the hit window.
    /// Schedules a repeat attack if configured and players remain inside.
    /// </summary>
    public void OnAttackEndFrame()
    {
        lastAttackTime = Time.time;
        isWindingUp = false;

        if (repeatWhileInside)
        {
            if (AnyPlayerPresent() && !awaitingNextAttack)
            {
                StartCoroutine(WaitAndRestartAttack());
            }
        }
    }

    private IEnumerator WaitAndRestartAttack()
    {
        awaitingNextAttack = true;

        float target = lastAttackTime + attackCooldown;
        while (Time.time < target)
            yield return null;

        awaitingNextAttack = false;

        if (!isWindingUp)
        {
            if (AnyPlayerPresent() && Time.time - lastAttackTime >= attackCooldown)
            {
                StartCoroutine(WindupCoroutine());
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (attackArea != null)
        {
            Gizmos.color = Color.red;
            var box = attackArea as BoxCollider2D;
            if (box != null)
            {
                Vector3 center = attackArea.transform.TransformPoint(box.offset);
                Vector3 size = new Vector3(box.size.x * attackArea.transform.lossyScale.x,
                    box.size.y * attackArea.transform.lossyScale.y, 0f);
                Gizmos.DrawWireCube(center, size);
            }
            else
            {
                Gizmos.DrawWireCube(attackArea.bounds.center, attackArea.bounds.size);
            }
        }
    }
#endif
}