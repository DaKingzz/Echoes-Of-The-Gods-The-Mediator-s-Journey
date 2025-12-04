using UnityEngine;
using UnityEngine.SceneManagement;

public class BossPortal : MonoBehaviour
{
    public string requiredKeyName;
    public string destinationBossScene;
    public bool IsPlayerTouching { get; private set; } = false;


    private void Start()
    {
        // If player already used this key in a previous scene
        if (InventoryManager.Instance.IsKeyAlreadyUsed(requiredKeyName))
        {
            UnlockPortal();
            //Debug.Log($"Portal {requiredKeyName} already unlocked previously.");
        }
    }

    public void UnlockPortal()
    {
        Debug.Log($"Portal unlocked using {requiredKeyName}!");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        bool isFinalBossPortal = (destinationBossScene == "FinalBoss");
        if (isFinalBossPortal)
        {
            if (AchievementManager.Instance.AllRequiredBossesDefeated() && InventoryManager.Instance.IsKeyAlreadyUsed(requiredKeyName))
            {
                LoadDestinationBossScene();
            }
            else
            {
                Debug.Log("Boss defeated:" + AchievementManager.Instance.bossesDefeated);
                Debug.Log("Final boss locked! Defeat all 3 bosses first!");
            }
            return; 
        }


        if (InventoryManager.Instance.IsKeyAlreadyUsed(requiredKeyName))
        {
            LoadDestinationBossScene();
        }
        else
        {
            IsPlayerTouching = true;
        }
        Debug.Log($"player touching portal: {IsPlayerTouching}");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        IsPlayerTouching = false;
    }

    public void LoadDestinationBossScene()
    {
        SceneManager.LoadScene(destinationBossScene);
    }
}
