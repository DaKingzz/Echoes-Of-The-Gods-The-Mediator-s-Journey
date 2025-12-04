using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawn : MonoBehaviour
{
    public static string NextSpawnName = "FromLeft";

    void OnEnable()
    {
        // Subscribe to sceneLoaded to spawn player after scene fully loads
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        var spawnRoot = GameObject.Find("SpawnPoints");
        if (spawnRoot == null)
        {
            Debug.LogWarning("[PlayerSpawn] SpawnPoints root not found!");
            return;
        }

        var target = spawnRoot.transform.Find(NextSpawnName);
        if (target == null)
        {
            Debug.LogWarning($"[PlayerSpawn] Spawn point '{NextSpawnName}' not found. Using default 'FromLeft'.");
            target = spawnRoot.transform.Find("FromLeft");
        }

        if (target == null)
        {
            Debug.LogError("[PlayerSpawn] Default spawn 'FromLeft' not found! Player will not be moved.");
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = target.position;
            Debug.Log($"[PlayerSpawn] Player spawned at '{target.name}' (NextSpawnName='{NextSpawnName}')");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawn] Player object not found!");
        }
    }


}
