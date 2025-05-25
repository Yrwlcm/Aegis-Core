using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public string nextLevelSceneName;
    
    public void StartGame()
    {
        SceneManager.LoadScene(nextLevelSceneName);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}