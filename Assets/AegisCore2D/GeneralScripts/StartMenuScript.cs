using UnityEngine;
using UnityEngine.SceneManagement;

namespace AegisCore2D.GeneralScripts
{
    public class StartMenuScript : MonoBehaviour
    {
        public void PlayGame()
        {
            SceneManager.LoadScene("SampleScene");
        }

        public void QuitGame()
        {
            Debug.Log("Игра закрывается");
            Application.Quit();
        }
    }
}