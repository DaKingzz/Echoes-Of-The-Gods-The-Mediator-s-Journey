using System;
using Unity.VisualScripting;
using UnityEngine;

public class HomingProjectileController : MonoBehaviour
{
    [Header("Projectile Stats")] [SerializeField]
    private float lifetime = 5f;

    private float damage;
    private float speed;
    private float turnSpeed; // Degrees per second
    private float homingDuration; // How long it tracks before going straight
    private float minDistanceToHome; // Stop homing if too close

    private Transform target;
    private Vector2 direction;
    private Rigidbody2D rb;
    private float homingTimer;
    private bool isHoming = true;

    private AudioSource launchAudio;
    private AudioSource homingAudio;
    private AudioSource impactAudio;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Initialize the homing projectile with all necessary parameters
    /// </summary>
    public void Initialize(
        Vector2 initialDirection,
        float projectileSpeed,
        float projectileDamage,
        float projectileTurnSpeed,
        float projectileHomingDuration,
        float projectileMinDistance,
        Transform targetTransform,
        AudioSource launchAudio,
        AudioSource homingAudio,
        AudioSource impactAudio)
    {
        // Set all properties
        direction = initialDirection.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        turnSpeed = projectileTurnSpeed;
        homingDuration = projectileHomingDuration;
        minDistanceToHome = projectileMinDistance;
        target = targetTransform;
        this.launchAudio = launchAudio;
        this.homingAudio = homingAudio;
        this.impactAudio = impactAudio;

        // Set initial rotation
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Set initial velocity
        rb.velocity = direction * speed;

        homingTimer = homingDuration;

        // Play launch audio
        AudioSource.PlayClipAtPoint(launchAudio.clip, transform.position);

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!isHoming || target == null)
        {
            // Move straight if not homing
            rb.velocity = direction * speed;
            return;
        }

        // Check if we should stop homing
        homingTimer -= Time.fixedDeltaTime;
        if (homingTimer <= 0f)
        {
            isHoming = false;
            homingAudio.Stop();
            return;
        }

        if (isHoming && !homingAudio.isPlaying)
        {
            homingAudio.Play();
        }

        // Calculate distance to target
        float distanceToTarget = Vector2.Distance(transform.position, target.position);
        if (distanceToTarget < minDistanceToHome)
        {
            // Too close, stop homing to allow dodging
            homingAudio.Stop();
            isHoming = false;
            return;
        }

        // Calculate direction to target
        Vector2 targetDirection = (target.position - transform.position).normalized;

        // Calculate the angle difference
        float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;

        // Smoothly rotate towards target with limited turn speed
        float angleStep = turnSpeed * Time.fixedDeltaTime;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, angleStep);

        // Update direction based on new angle
        float newAngleRad = newAngle * Mathf.Deg2Rad;
        direction = new Vector2(Mathf.Cos(newAngleRad), Mathf.Sin(newAngleRad));

        // Update rotation
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);

        // Update velocity
        rb.velocity = direction * speed;
    }

    private void OnDestroy()
    {
        launchAudio.Stop();
        homingAudio.Stop();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<IPlayer>();
        if (player != null)
        {
            player.TakeDamage(damage);
            impactAudio.Play();
            Destroy(gameObject);
        }
    }

    // Optional: Visual debug to see homing status
    private void OnDrawGizmos()
    {
        if (isHoming && target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}