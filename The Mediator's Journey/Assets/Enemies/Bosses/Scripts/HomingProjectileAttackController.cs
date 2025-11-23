using UnityEngine;

public class HomingProjectileAttackController : MonoBehaviour
{
    [Header("Projectile Settings")] [SerializeField]
    private GameObject projectilePrefab;

    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 30f;

    [Header("Attack Settings")] [SerializeField, Range(0f, 1f)]
    private float precision = 0.9f;

    [SerializeField] private float attackCooldown = 5f;

    [Header("Homing Settings")] [SerializeField]
    private bool useHomingProjectiles = true;

    [SerializeField] private float homingTurnSpeed = 120f;
    [SerializeField] private float homingDuration = 3f;

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
            if (homing != null)
            {
                homing.Initialize(dir, projectileSpeed, target);

                // Override homing settings if needed
                // You can expose these through reflection or make them public in HomingProjectileController
            }
            else
            {
                Debug.LogWarning("Homing projectile enabled but HomingProjectileController not found on prefab!");
                // Fallback to regular projectile
                ProjectileController regular = projectile.GetComponent<ProjectileController>();
                if (regular != null)
                {
                    regular.Initialize(dir, projectileSpeed);
                }
            }
        }
        else
        {
            // Use regular projectile
            ProjectileController regular = projectile.GetComponent<ProjectileController>();
            if (regular != null)
            {
                regular.Initialize(dir, projectileSpeed);
            }
            else
            {
                Debug.LogWarning("ProjectileController not found on prefab!");
            }
        }

        return true;
    }
}