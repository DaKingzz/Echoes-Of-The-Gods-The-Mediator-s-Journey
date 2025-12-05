using UnityEngine;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    public int bossesDefeated = 0; 

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void MarkBossDefeated()
    {
        bossesDefeated++;
        Debug.Log("Boss defeated! Total bosses defeated = " + bossesDefeated);
    }

    public bool AllRequiredBossesDefeated()
    {
        return bossesDefeated >= 3; // You need 3 to unlock final boss
    }
}
