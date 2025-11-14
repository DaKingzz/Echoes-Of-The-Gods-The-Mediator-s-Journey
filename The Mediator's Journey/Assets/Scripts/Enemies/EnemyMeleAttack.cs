using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyMeleeAttackAnimDriven
/// - Starts windup when a player enters the attackArea trigger.
/// - After windup it fires a cached animator trigger to play the attack animation.
/// - The animation must call OnAttackHitFrame() at the frame when the weapon is extended,
///   and OnAttackEndFrame() at the frame when the attack window closes.
/// - OnAttackHitFrame applies damage to any IPlayer currently inside the attackArea.
/// - Damage, timings and the attackArea trigger are configurable in the inspector.
/// - Uses a cached animator parameter id: IsAttacking.
/// - Optionally repeats attacks while the player remains inside the trigger (repeatWhileInside).
/// </summary>
[DisallowMultipleComponent]
public class EnemyMeleeAttackAnimDriven : MonoBehaviour
{
    [Header("Timing")] [Tooltip("Windup time from player entering until animation trigger.")] [SerializeField]
    private float windupDelay = 0.35f;

    [Tooltip("Minimum time between attacks.")] [SerializeField]
    private float attackCooldown = 1.0f;

    [Header("Damage")] [Tooltip("Damage applied to the player when hit.")] [SerializeField]
    private float damage = 5f;

    [Header("Attack Area (trigger)")]
    [Tooltip("Trigger collider that represents reach of the attack. Should be IsTrigger = true.")]
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

    private const string playerTag = "Player";

    // runtime state
    private readonly HashSet<IPlayer> playersInside = new HashSet<IPlayer>();
    private volatile bool isWindingUp = false;
    private float lastAttackTime = -Mathf.Infinity;

    // prevents multiple concurrent restart waiters
    private bool awaitingNextAttack = false;

    private void Reset()
    {
        if (attackArea == null) attackArea = GetComponentInChildren<Collider2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (attackArea == null)
            Debug.LogError($"{nameof(EnemyMeleeAttackAnimDriven)} requires an attackArea trigger Collider2D.");

        if (attackArea != null && !attackArea.isTrigger)
            Debug.LogWarning(
                $"{nameof(EnemyMeleeAttackAnimDriven)}: attackArea should be a trigger collider (IsTrigger = true).");

        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        var p = other.GetComponentInParent<IPlayer>();
        if (p == null) return;

        lock (playersInside)
        {
            playersInside.Add(p);
        }

        // Start windup if not already in one and cooldown passed
        if (!isWindingUp && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(WindupCoroutine());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        var p = other.GetComponentInParent<IPlayer>();
        if (p == null) return;

        lock (playersInside)
        {
            playersInside.Remove(p);
        }
    }

    private IEnumerator WindupCoroutine()
    {
        isWindingUp = true;

        float start = Time.time;
        while (Time.time - start < windupDelay)
        {
            if (cancelIfEmptyDuringWindup)
            {
                lock (playersInside)
                {
                    if (playersInside.Count == 0)
                    {
                        isWindingUp = false;
                        yield break;
                    }
                }
            }

            yield return null;
        }

        // Trigger animator using cached hash
        if (animator != null)
            animator.SetTrigger(IsAttacking);

        // Keep isWindingUp true until OnAttackEndFrame is called by animation event
    }

    /// <summary>
    /// Animation event: called on the frame where the weapon is extended.
    /// Applies damage to any IPlayer currently inside the attackArea.
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (!isWindingUp) return;

        IPlayer[] snapshot;
        lock (playersInside)
        {
            snapshot = new IPlayer[playersInside.Count];
            playersInside.CopyTo(snapshot);
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            var p = snapshot[i];
            p?.TakeDamage(damage);
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

        // schedule another attempt after cooldown if requested and there are players inside
        if (repeatWhileInside)
        {
            bool hasPlayers;
            lock (playersInside)
            {
                hasPlayers = playersInside.Count > 0;
            }

            if (hasPlayers && !awaitingNextAttack)
            {
                StartCoroutine(WaitAndRestartAttack());
            }
        }
    }

    private IEnumerator WaitAndRestartAttack()
    {
        awaitingNextAttack = true;

        // wait until cooldown expires (use lastAttackTime as reference)
        float waitStart = Time.time;
        float target = lastAttackTime + attackCooldown;
        // If lastAttackTime updated again externally, target will reflect the latest (we always wait until at least target)
        while (Time.time < target)
            yield return null;

        awaitingNextAttack = false;

        // Start a new windup only if not currently winding and players are still inside
        if (!isWindingUp)
        {
            bool hasPlayers;
            lock (playersInside)
            {
                hasPlayers = playersInside.Count > 0;
            }

            if (hasPlayers && Time.time - lastAttackTime >= attackCooldown)
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