using UnityEngine;
using UnityEngine.SceneManagement;

public class BossPortal : MonoBehaviour
{
    public string requiredKeyName;
    public string destinationBossScene;
    public string destinationSpawnName;
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

        PlayerSpawn.NextSpawnName = string.IsNullOrEmpty(destinationSpawnName) ? "FromLeft" : destinationSpawnName;

        bool isFinalBossPortal = (destinationBossScene == "FinalBoss");

        if (isFinalBossPortal)
        {
            if (AchievementManager.Instance.AllRequiredBossesDefeated())
            {
                if (InventoryManager.Instance.IsKeyAlreadyUsed(requiredKeyName))
                {
                    Debug.Log("Final boss unlocked!");
                    LoadDestinationBossScene();
                }
                else
                {
                    IsPlayerTouching = true; 
                    Debug.Log("Final boss portal ready. Use correct key.");
                }
            }
            else
            {
                Debug.Log("Final boss locked! Defeat all 3 bosses first!");
            }
            return; // stop further logic for final boss
        }

        // Normal bosses
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene(destinationBossScene);
        }
        else
        {
            Debug.LogWarning("GameManager not found, falling back to SceneManager.");
            SceneManager.LoadScene(destinationBossScene, LoadSceneMode.Single);
        }

    }
}
