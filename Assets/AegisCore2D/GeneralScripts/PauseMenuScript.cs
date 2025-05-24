using UnityEngine;

namespace AegisCore2D.GeneralScripts
{
    public class PauseMenu : MonoBehaviour
    {
        public GameObject pauseMenuUI;
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
            pauseMenuUI.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
        }

        private void Pause()
        {
            pauseMenuUI.SetActive(true);
            Time.timeScale = 0f;
            isPaused = true;
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
            Application.Quit();
        }
    }
}