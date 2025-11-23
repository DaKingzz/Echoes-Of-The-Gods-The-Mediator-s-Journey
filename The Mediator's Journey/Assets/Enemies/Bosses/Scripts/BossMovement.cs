using System.Collections;
using System.Collections.Generic;
using Unity.PlasticSCM.Editor.WebApi;
using UnityEngine;

/// <summary>
/// Simple boss movement with two modes:
/// - Loop: visit waypoints in order, looping.
/// - RandomWalk: pick a waypoint at random (optionally biased toward player).
/// 
/// Notes:
/// - No rotation is written anywhere.
/// - No curves. Movement uses a single speed value and deterministic MovePosition.
/// - Optional visualRoot compensates for scaled visuals (waypoints target the visual).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossMovement : MonoBehaviour, IEnemy
{
    public enum MovementMode
    {
        Loop,
        RandomWalk
    }

    [Header("Core")] public MovementMode mode = MovementMode.Loop;

    [Tooltip("Ordered world-space points the boss will visit in sequence.")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Tooltip("Optional: the Transform to treat as the visual pivot. If set, waypoints target this visual position;\n" +
             "the physics/root will be moved so the visual aligns with the waypoint (useful when visual is offset or scaled).")]
    [SerializeField] private Transform visualRoot;

    [Header("Motion")] [Tooltip("Movement speed in world units per second.")]
    [SerializeField] private float speed = 3f;

    [Tooltip("How close (world units) we must be to consider the waypoint reached.")]
    [SerializeField] private float arrivalThreshold = 0.05f;

    [Header("Timing")] [Tooltip("Pause at waypoint (seconds).")]
    [SerializeField] private float pauseAtWaypoint = 0.25f;

    [Header("Random")] [Range(0f, 1f)] public float randomBiasTowardsPlayer = 0.4f;
    [SerializeField] private Transform player;

    [Header("Behavior")]
    [Tooltip("If true the waypoint list loops (Loop mode). RandomWalk always continues picking points.")]
    [SerializeField] private bool loop = true;
    
    [Header("Health")]
    [SerializeField] private float maxHealth = 30f;
    private float currentHealth;

    // Runtime
    Rigidbody2D rb;
    int index = 0;
    System.Random rng;

    BossMovement()
    {
        currentHealth = maxHealth;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rng = new System.Random();

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

    IEnumerator LoopRoutine()
    {
        while (mode == MovementMode.Loop)
        {
            yield return MoveToWaypoint(index);
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

        Vector2 visualTarget = waypoints[waypointIndex].position;
        while (true)
        {
            Vector2 currentRoot = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 rootTarget = RootPositionForVisualWorldTarget(visualTarget);
            float dist = Vector2.Distance(currentRoot, rootTarget);
            if (dist <= arrivalThreshold) break;
            Vector2 next = Vector2.MoveTowards(currentRoot, rootTarget, speed * Time.deltaTime);
            if (rb != null) rb.MovePosition(next);
            else transform.position = (Vector3)next;
            yield return null;
        }

        // snap exactly
        Vector2 finalRoot = RootPositionForVisualWorldTarget(visualTarget);
        if (rb != null) rb.position = finalRoot;
        else transform.position = (Vector3)finalRoot;

        yield break;
    }

    // Helper to compute which root position (physics root) will place visualRoot at desiredVisualWorldPos
    Vector2 RootPositionForVisualWorldTarget(Vector2 desiredVisualWorldPos)
    {
        if (visualRoot == null)
            return desiredVisualWorldPos;

        Vector2 visualWorldPos = visualRoot.position;
        Vector2 rootWorldPos = transform.position;
        Vector2 offset = visualWorldPos - rootWorldPos;
        return desiredVisualWorldPos - offset;
    }

    Vector2 GetVisualPosition()
    {
        return visualRoot != null ? (Vector2)visualRoot.position : (Vector2)transform.position;
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

    public bool TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
            return true;
        }

        return false;
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
            Vector3 bossPos = GetVisualPosition();
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