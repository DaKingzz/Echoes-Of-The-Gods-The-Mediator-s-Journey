using UnityEngine;

public class PlayerSpawn : MonoBehaviour
{
    public static string NextSpawnName = "FromLeft";

    void Start()
    {
        var spawnRoot = GameObject.Find("SpawnPoints");
        if (spawnRoot == null) return;

        var target = spawnRoot.transform.Find(NextSpawnName);
        if (target == null) target = spawnRoot.transform.Find("FromLeft");

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && target != null)
            player.transform.position = target.position;
    }
}
