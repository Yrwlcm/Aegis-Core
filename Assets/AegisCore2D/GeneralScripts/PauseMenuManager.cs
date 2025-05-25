// PauseMenuManager.cs (или переименуй/дополни свой PauseMenu.cs)

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Для InputSystem_Actions

namespace AegisCore2D.GeneralScripts // Используй свой неймспейс
{
    public class PauseMenuManager : MonoBehaviour // Измени имя класса, если нужно
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject controlsPanelInGame; // Панель управления в игре

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button controlsButtonPause;
        [SerializeField] private Button exitToMenuButton;
        [SerializeField] private Button backButtonFromControlsInGame;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        private bool isPaused = false;
        private InputSystem_Actions inputActions; // Твой класс Input Actions

        private void Awake()
        {
            inputActions = new InputSystem_Actions();
            // Подписка на событие Pause из Input Actions Asset
            inputActions.UI.Pause.performed += _ => TogglePause();

            // Скрываем панели при старте
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (controlsPanelInGame != null) controlsPanelInGame.SetActive(false);

            // Назначаем слушателей кнопкам
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (controlsButtonPause != null) controlsButtonPause.onClick.AddListener(ShowControlsInGame);
            if (exitToMenuButton != null) exitToMenuButton.onClick.AddListener(ExitToMainMenu);
            if (backButtonFromControlsInGame != null) backButtonFromControlsInGame.onClick.AddListener(HideControlsInGame);
            
            StartCoroutine(ScreenFader.Instance.FadeIn(5));
        }

        private void OnEnable() => inputActions?.UI.Enable();
        private void OnDisable() => inputActions?.UI.Disable();

        private void TogglePause()
        {
            if (controlsPanelInGame != null && controlsPanelInGame.activeSelf)
            {
                // Если открыта панель управления, кнопка Esc должна ее закрывать, а не меню паузы
                HideControlsInGame();
                return;
            }

            if (isPaused) Resume();
            else Pause();
        }

        public void Resume() // Сделаем public, если нужно вызывать из кнопки
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
        }

        private void Pause()
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
            Time.timeScale = 0f; // Ставим игру на паузу
            isPaused = true;
        }

        private void ShowControlsInGame()
        {
            if (controlsPanelInGame != null) controlsPanelInGame.SetActive(true);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false); // Скрываем меню паузы
        }

        private void HideControlsInGame()
        {
            if (controlsPanelInGame != null) controlsPanelInGame.SetActive(false);
            if (pauseMenuPanel != null && isPaused) pauseMenuPanel.SetActive(true); // Показываем меню паузы обратно, если были на паузе
        }

        public void ExitToMainMenu() // Сделаем public
        {
            Time.timeScale = 1f; // ОБЯЗАТЕЛЬНО восстанавливаем Time.timeScale перед сменой сцены
            isPaused = false;

            if (ScreenFader.Instance != null)
            {
                StartCoroutine(FadeAndLoadScene(mainMenuSceneName));
            }
            else
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private IEnumerator FadeAndLoadScene(string sceneName)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeOut());
            SceneManager.LoadScene(sceneName);
        }

        private void OnDestroy()
        {
            if (inputActions != null)
            {
                inputActions.UI.Pause.performed -= _ => TogglePause();
                inputActions.UI.Disable();
            }
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (controlsButtonPause != null) controlsButtonPause.onClick.RemoveListener(ShowControlsInGame);
            if (exitToMenuButton != null) exitToMenuButton.onClick.RemoveListener(ExitToMainMenu);
            if (backButtonFromControlsInGame != null) backButtonFromControlsInGame.onClick.RemoveListener(HideControlsInGame);
        }
    }
}