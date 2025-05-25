using UnityEngine;
using UnityEngine.SceneManagement;

namespace AegisCore2D.GeneralScripts
{
    public class StartMenuScript : MonoBehaviour
    {
        public void PlayGame()
        {
            // Consider making scene name a [SerializeField] string for flexibility
            SceneManager.LoadScene("SampleScene");
        }

        public void QuitGame()
        {
            // Debug.Log("Quitting Game..."); // Optional log
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}