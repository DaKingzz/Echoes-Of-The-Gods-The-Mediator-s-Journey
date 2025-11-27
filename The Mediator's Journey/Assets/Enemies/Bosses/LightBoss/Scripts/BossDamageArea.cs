using UnityEngine;

/// <summary>
/// BossDamageArea
/// - Helper component for the boss's damage area child GameObject.
/// - Forwards trigger events to the parent WalkingBoss script.
/// - Attach this to a child GameObject with a Collider2D set as trigger.
/// </summary>
public class BossDamageArea : MonoBehaviour
{
    private WalkingBoss walkingBoss;

    private void Awake()
    {
        walkingBoss = GetComponentInParent<WalkingBoss>();

        if (walkingBoss == null)
        {
            Debug.LogError("BossDamageArea could not find WalkingBoss component in parent!", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (walkingBoss != null)
        {
            walkingBoss.OnDamageAreaEnter(collision);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (walkingBoss != null)
        {
            walkingBoss.OnDamageAreaStay(collision);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (walkingBoss != null)
        {
            walkingBoss.OnDamageAreaExit(collision);
        }
    }
}