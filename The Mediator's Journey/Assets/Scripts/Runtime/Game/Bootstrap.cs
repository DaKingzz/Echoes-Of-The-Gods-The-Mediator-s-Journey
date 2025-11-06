using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    void Start()
    {
        GameManager.Instance.LoadMainMenu();
    }
}
