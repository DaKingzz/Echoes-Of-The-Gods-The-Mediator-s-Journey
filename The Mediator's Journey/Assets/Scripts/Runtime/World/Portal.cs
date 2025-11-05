using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    public string destinationScene;       // ex: "Game" , has to be the scene name in build settings
    public string destinationSpawnName;   // ex: "FromLeft", has to be one the empty spawnpoints in RoomRoot

    bool loading;

    void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (loading) return;
        if (!other.CompareTag("Player")) return;

        loading = true;

        // Tell next scene where to drop the player
        PlayerSpawn.NextSpawnName = string.IsNullOrEmpty(destinationSpawnName) ? "FromLeft" : destinationSpawnName;

        // Use GameManager if present; otherwise fall back to SceneManager, this was causing problems...
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene(destinationScene);
        }
        else
        {
            Debug.LogWarning("[Portal] GameManager not found. Falling back to SceneManager.LoadScene.", this);
            SceneManager.LoadScene(destinationScene, LoadSceneMode.Single);
        }
    }
}
