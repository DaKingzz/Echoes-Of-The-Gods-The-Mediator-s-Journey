using UnityEngine;

public class EnemyFireballAttack : MonoBehaviour
{
    [Header("Fireball Settings")] [SerializeField]
    private GameObject fireballPrefab;

    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireballSpeed = 30f;
    [SerializeField, Range(0f, 1f)] private float precision = 0.9f;
    [SerializeField] private float attackCooldown = 5f;

    private float lastAttackTime;
    public Transform target;

    public bool TryAttack()
    {
        if (target == null) return false;
        if (Time.time - lastAttackTime < attackCooldown) return false;

        lastAttackTime = Time.time;

        // Aim with precision factor
        Vector2 toTarget = (target.position - firePoint.position);
        float angleError = (1f - precision) * Random.Range(-15f, 15f); // degrees
        Vector2 dir = Quaternion.Euler(0, 0, angleError) * toTarget.normalized;

        // Spawn fireball
        GameObject fb = Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);
        fb.GetComponent<Fireball>().Initialize(dir, fireballSpeed);
        return true;
    }
}