using UnityEngine;

public class GravityController : MonoBehaviour
{
    public static GravityController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GravityController>();
                if (_instance == null)
                {
                    var go = new GameObject("GravityController");
                    _instance = go.AddComponent<GravityController>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    static GravityController _instance;

    public float Gravity = -9.81f;
}