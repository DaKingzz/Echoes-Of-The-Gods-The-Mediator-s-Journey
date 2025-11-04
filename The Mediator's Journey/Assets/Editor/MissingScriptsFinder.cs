#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MissingScriptsFinder
{
    [MenuItem("Tools/Find Missing Scripts In Scene")]
    static void FindMissingScriptsInScene()
    {
        int goCount = 0, compCount = 0, missingCount = 0;
        var all = Object.FindObjectsOfType<GameObject>(true);
        foreach (var go in all)
        {
            goCount++;
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                compCount++;
                if (comps[i] == null)
                {
                    missingCount++;
                    Debug.LogWarning($"Missing script on: {GetPath(go)}", go);
                }
            }
        }
        Debug.Log($"Scanned {goCount} GameObjects / {compCount} components > Missing: {missingCount}");
    }

    static string GetPath(GameObject go)
    {
        return go.transform.parent == null ? go.name : GetPath(go.transform.parent.gameObject) + "/" + go.name;
    }
}
#endif
