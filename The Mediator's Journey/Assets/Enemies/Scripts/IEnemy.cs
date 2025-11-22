public interface IEnemy
{
    /// <summary>
    /// Applies damage to the enemy.
    /// </summary>
    /// <param name="damage"></param>
    /// <returns>True if enemy is killed</returns>
    bool TakeDamage(float damage);
}
