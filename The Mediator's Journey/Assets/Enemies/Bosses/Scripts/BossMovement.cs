using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple boss movement with two modes:
/// - Loop: visit waypoints in order, looping.
/// - RandomWalk: pick a waypoint at random (optionally biased toward player).
/// 
/// Notes:
/// - No rotation is written anywhere.
/// - No curves. Movement uses a single speed value and deterministic MovePosition.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class BossMovement : MonoBehaviour, IEnemy
{
    public enum MovementMode
    {
        Loop,
        RandomWalk
    }

    [Header("Core")] public MovementMode mode = MovementMode.Loop;

    [Tooltip("Ordered world-space points the boss will visit in sequence.")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Motion")] [Tooltip("Movement speed in world units per second.")]
    public float speed = 3f;

    [Tooltip("How close (world units) we must be to consider the waypoint reached.")]
    public float arrivalThreshold = 0.05f;

    [Header("Timing")] [Tooltip("Pause at waypoint (seconds).")]
    public float pauseAtWaypoint = 0.25f;

    [Header("Random")] [Range(0f, 1f)] public float randomBiasTowardsPlayer = 0.4f;
    public Transform player;

    [Header("Behavior")]
    [Tooltip("If true the waypoint list loops (Loop mode). RandomWalk always continues picking points.")]
    public bool loop = true;

    [Header("Health")] [Tooltip("Maximum health of the boss")]
    public float maxHealth = 100f;

    [Header("Attack")] [Tooltip("Reference to the attack component")] [SerializeField]
    private HomingProjectileAttackController attackComponent;

    [Tooltip("Should boss attack at each waypoint?")] [SerializeField]
    private bool attackAtWaypoints = true;

    [Tooltip("Should boss attack while moving?")] [SerializeField]
    private bool attackWhileMoving = true;

    [Tooltip("Time between mid-movement attacks")] [SerializeField]
    private float movementAttackInterval = 2f;

    private float lastMovementAttackTime;

    // Runtime
    Rigidbody2D rb;
    Animator animator;
    int index = 0;
    System.Random rng = new System.Random();
    private float currentHealth;

    // Animation parameter IDs (cached for performance)
    private int isDeadHash;
    private int isHitHash;

    void Start()
    {
        currentHealth = maxHealth;

        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Cache animation parameter IDs
        isDeadHash = Animator.StringToHash("isDead");
        isHitHash = Animator.StringToHash("isHit");

        // Get attack component if not assigned

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    void OnEnable()
    {
        index = 0;
        StopAllCoroutines();
        StartCoroutine(BehaviorLoop());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator BehaviorLoop()
    {
        while (true)
        {
            if ((waypoints == null || waypoints.Count == 0))
            {
                // Idle: stay near current visual position
                yield return null;
            }
            else
            {
                switch (mode)
                {
                    case MovementMode.Loop:
                        yield return StartCoroutine(LoopRoutine());
                        break;
                    case MovementMode.RandomWalk:
                        yield return StartCoroutine(RandomWalkRoutine());
                        break;
                }
            }

            yield return null;
        }
    }

    public bool TakeDamage(float amount)
    {
        currentHealth -= amount;

        // Trigger hit animation
        if (animator != null)
        {
            animator.SetTrigger(isHitHash);
        }

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;

            // Set death animation
            if (animator != null)
            {
                animator.SetBool(isDeadHash, true);
            }

            // Boss defeated
            StopAllCoroutines();
            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    IEnumerator LoopRoutine()
    {
        while (mode == MovementMode.Loop)
        {
            yield return MoveToWaypoint(index);

            // Attack at waypoint if enabled
            if (attackAtWaypoints && attackComponent != null)
            {
                attackComponent.TryAttack();
            }

            // Advance index and wrap
            index = (index + 1) % Mathf.Max(1, waypoints.Count);
            yield return new WaitForSeconds(pauseAtWaypoint);
        }
    }

    IEnumerator RandomWalkRoutine()
    {
        while (mode == MovementMode.RandomWalk)
        {
            if (waypoints.Count == 0) yield return null;
            int next = rng.Next(0, waypoints.Count);
            if (player != null && rng.NextDouble() < randomBiasTowardsPlayer)
                next = ClosestWaypointIndexTo(player.position);
            index = next;
            yield return MoveToWaypoint(index);

            // Attack at waypoint if enabled
            if (attackAtWaypoints && attackComponent != null)
            {
                attackComponent.TryAttack();
            }

            yield return new WaitForSeconds(pauseAtWaypoint * (0.5f + (float)rng.NextDouble()));
        }
    }

    // Move root so that the visual (visualRoot or root) reaches the waypoint at index
    IEnumerator MoveToWaypoint(int waypointIndex)
    {
        if (waypoints == null || waypointIndex < 0 || waypointIndex >= waypoints.Count)
            yield break;
        if (waypoints[waypointIndex] == null)
            yield break;

        Vector2 target = waypoints[waypointIndex].position;
        while (true)
        {
            Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
            float dist = Vector2.Distance(currentPos, target);
            if (dist <= arrivalThreshold) break;
            Vector2 next = Vector2.MoveTowards(currentPos, target, speed * Time.deltaTime);
            if (rb != null) rb.MovePosition(next);
            else transform.position = (Vector3)next;

            // Try to attack while moving if enabled
            if (attackWhileMoving && attackComponent != null &&
                Time.time - lastMovementAttackTime >= movementAttackInterval)
            {
                if (attackComponent.TryAttack())
                {
                    lastMovementAttackTime = Time.time;
                }
            }

            yield return null;
        }

        // snap exactly
        if (rb != null) rb.position = target;
        else transform.position = (Vector3)target;

        yield break;
    }

    int ClosestWaypointIndexTo(Vector2 worldPos)
    {
        if (waypoints == null || waypoints.Count == 0) return 0;
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            float d = Vector2.SqrMagnitude((Vector2)waypoints[i].position - worldPos);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    [Header("Editor Visualization")] [Tooltip("Color for waypoint spheres and path lines")]
    public Color waypointColor = Color.cyan;

    [Tooltip("Size of waypoint spheres")] public float waypointGizmoSize = 0.3f;

    [Tooltip("Show waypoint labels with their names")]
    public bool showWaypointLabels = true;

    [Tooltip("Show path lines connecting waypoints")]
    public bool showPathLines = true;

    [Tooltip("Show current target waypoint differently")]
    public bool highlightCurrentTarget = true;

    void OnDrawGizmos()
    {
        // Draw always, not just when selected
        DrawWaypointGizmos(false);
    }

    void OnDrawGizmosSelected()
    {
        // Draw with highlighting when selected
        DrawWaypointGizmos(true);
    }

    void DrawWaypointGizmos(bool isSelected)
    {
        if (waypoints == null || waypoints.Count == 0) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            Vector3 waypointPos = waypoints[i].position;

            // Determine if this is the current target
            bool isCurrent = Application.isPlaying && highlightCurrentTarget && i == index;

            // Set color based on state
            if (isCurrent)
            {
                Gizmos.color = Color.yellow;
                UnityEditor.Handles.color = Color.yellow;
            }
            else
            {
                Gizmos.color = waypointColor;
                UnityEditor.Handles.color = waypointColor;
            }

            // Draw waypoint sphere (filled if current, wire if not)
            if (isCurrent)
            {
                Gizmos.DrawSphere(waypointPos, waypointGizmoSize * 0.8f);
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(waypointPos, waypointGizmoSize);
            }
            else
            {
                Gizmos.DrawWireSphere(waypointPos, waypointGizmoSize);
            }

            // Draw waypoint number and name
            if (showWaypointLabels)
            {
                UnityEditor.Handles.Label(
                    waypointPos + Vector3.up * (waypointGizmoSize + 0.2f),
                    $"[{i}] {waypoints[i].name}",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = isCurrent ? Color.yellow : waypointColor },
                        fontSize = 12,
                        fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter
                    }
                );
            }

            // Draw path lines
            if (showPathLines)
            {
                Gizmos.color = waypointColor * 0.7f;

                // Line to next waypoint
                if (i + 1 < waypoints.Count && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypointPos, waypoints[i + 1].position);

                    // Draw arrow direction indicator
                    if (isSelected)
                    {
                        Vector3 direction = (waypoints[i + 1].position - waypointPos).normalized;
                        Vector3 midPoint = Vector3.Lerp(waypointPos, waypoints[i + 1].position, 0.5f);
                        DrawArrow(midPoint, direction, 0.3f);
                    }
                }
            }
        }

        // Loop back line
        if (showPathLines && loop && waypoints.Count > 1 && waypoints[0] != null &&
            waypoints[waypoints.Count - 1] != null)
        {
            Gizmos.color = waypointColor * 0.5f;
            Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, waypoints[0].position);

            if (isSelected)
            {
                Vector3 direction = (waypoints[0].position - waypoints[waypoints.Count - 1].position).normalized;
                Vector3 midPoint = Vector3.Lerp(waypoints[waypoints.Count - 1].position, waypoints[0].position, 0.5f);
                DrawArrow(midPoint, direction, 0.3f);
            }
        }

        // Draw line from boss to current target when playing
        if (Application.isPlaying && highlightCurrentTarget && index < waypoints.Count && waypoints[index] != null)
        {
            Vector3 bossPos = rb != null ? (Vector3)rb.position : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(bossPos, waypoints[index].position);
        }
    }

    void DrawArrow(Vector3 position, Vector3 direction, float size)
    {
        Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
        if (right == Vector3.zero) right = Vector3.Cross(direction, Vector3.up).normalized;

        Vector3 arrowTip = position + direction * size * 0.5f;
        Vector3 arrowLeft = position - direction * size * 0.3f + right * size * 0.3f;
        Vector3 arrowRight = position - direction * size * 0.3f - right * size * 0.3f;

        Gizmos.DrawLine(arrowTip, arrowLeft);
        Gizmos.DrawLine(arrowTip, arrowRight);
        Gizmos.DrawLine(arrowLeft, arrowRight);
    }
#endif
}