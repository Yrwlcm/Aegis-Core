using UnityEngine;
using UnityEngine.InputSystem; // Assuming InputSystem_Actions is generated in this namespace or accessible

namespace AegisCore2D.GeneralScripts
{
    public class PauseMenu : MonoBehaviour // Class name differs from file name "PauseMenuScript.cs"
    {
        [SerializeField] public GameObject pauseMenuUI; // Public if set in Inspector and potentially accessed elsewhere
        private bool isPaused = false;
        private InputSystem_Actions inputActions;

        private void Awake()
        {
            inputActions = new InputSystem_Actions();
            inputActions.UI.Pause.performed += _ => TogglePause();
        }

        private void OnEnable() => inputActions?.UI.Enable();
        private void OnDisable() => inputActions?.UI.Disable();

        private void TogglePause()
        {
            if (isPaused) Resume();
            else Pause();
        }

        private void Resume()
        {
            if (pauseMenuUI != null) pauseMenuUI.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
        }

        private void Pause()
        {
            if (pauseMenuUI != null) pauseMenuUI.SetActive(true);
            Time.timeScale = 0f;
            isPaused = true;
        }

        public void QuitGame()
        {
            Time.timeScale = 1f; // Reset time scale before quitting
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // Stops play mode in editor
#endif
        }
    }
}