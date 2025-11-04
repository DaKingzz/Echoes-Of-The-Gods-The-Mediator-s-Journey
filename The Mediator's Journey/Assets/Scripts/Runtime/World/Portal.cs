using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    public string destinationScene;       // ex: "Room_Up"
    public string destinationSpawnName;   // ex: "FromBottom"

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

        PlayerSpawn.NextSpawnName = destinationSpawnName;
        GameManager.Instance.LoadScene(destinationScene);
    }
}
