using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwordWeapon : MonoBehaviour
{
    [SerializeField] private float attackPower = 3f;
    [SerializeField] private LayerMask enemyMask; // assign in Inspector

    public float AttackPower => attackPower;
    public bool IsAttacking { get; private set; }

    private Animator animator;
    private readonly HashSet<Collider2D> hitThisAttack = new HashSet<Collider2D>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Attack(InputAction.CallbackContext context)
    {
        if (context.performed && !IsAttacking)
        {
            IsAttacking = true;
            hitThisAttack.Clear(); // reset hit list
            animator.SetTrigger("swordAttack");
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsAttacking) return;

        // Check if collider is on the enemy layer
        if (((1 << other.gameObject.layer) & enemyMask) == 0)return;

        // Only hit once per attack
        if (hitThisAttack.Contains(other)) return;

        IEnemy enemy = other.GetComponent<IEnemy>();
        if (enemy != null)
        {
            bool killed = enemy.TakeDamage(AttackPower);
            Debug.Log($"Hit {other.gameObject.name}, killed: {killed}");
            hitThisAttack.Add(other);
        }
    }

    // Called by animation event at end of swordAttack animation
    public void EndAttack()
    {
        IsAttacking = false;
        hitThisAttack.Clear();
    }
}