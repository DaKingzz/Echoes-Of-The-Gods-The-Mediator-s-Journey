using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuBindings : MonoBehaviour
{
    public void OnPlay() { GameManager.Instance.StartNewGame(); }
    public void OnQuit() => GameManager.Instance.Quit();
    public void OnMenu() => GameManager.Instance.LoadMainMenu();
}
