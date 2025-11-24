using UnityEngine;

public class HomingProjectileAttackController : MonoBehaviour
{
    [Header("Projectile Settings")] [SerializeField]
    private GameObject projectilePrefab;

    [SerializeField] private Transform firePoint;

    [Header("Projectile Stats")] [SerializeField]
    private float projectileSpeed = 30f;

    [SerializeField] private float projectileDamage = 1.5f;

    [Header("Attack Settings")] [SerializeField, Range(0f, 1f)]
    private float precision = 0.9f;

    [SerializeField] private float attackCooldown = 5f;

    [Header("Homing Settings")] [SerializeField]
    private bool useHomingProjectiles = true;

    [SerializeField] private float homingTurnSpeed = 120f;
    [SerializeField] private float homingDuration = 3f;
    [SerializeField] private float homingMinDistance = 0.5f;

    private float lastAttackTime;
    public Transform target;

    public bool TryAttack()
    {
        if (target == null) return false;
        if (Time.time - lastAttackTime < attackCooldown) return false;

        lastAttackTime = Time.time;

        // Calculate direction to target
        Vector2 toTarget = (target.position - firePoint.position);
        float angleError = (1f - precision) * Random.Range(-15f, 15f); // degrees
        Vector2 dir = Quaternion.Euler(0, 0, angleError) * toTarget.normalized;

        // Spawn projectile
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // Check if it's a homing projectile
        if (useHomingProjectiles)
        {
            HomingProjectileController homing = projectile.GetComponent<HomingProjectileController>();
            if (homing == null)
            {
                Debug.LogError("Homing projectile enabled but HomingProjectileController not found on prefab!");
                Destroy(projectile);
                return false;
            }

            homing.Initialize(
                dir, // Initial direction
                projectileSpeed, // Speed
                projectileDamage, // Damage
                homingTurnSpeed, // Turn speed
                homingDuration, // Homing duration
                homingMinDistance, // Minimum distance to stop homing
                target // Target transform
            );
        }
        else
        {
            // Use regular projectile
            ProjectileController regular = projectile.GetComponent<ProjectileController>();
            if (regular == null)
            {
                Debug.LogError("ProjectileController not found on prefab!");
                Destroy(projectile);
                return false;
            }

            regular.Initialize(dir, projectileSpeed);
        }

        return true;
    }
}