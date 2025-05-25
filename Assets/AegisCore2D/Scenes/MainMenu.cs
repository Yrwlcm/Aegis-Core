using System.Collections;
using AegisCore2D.GeneralScripts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button exitButton;

    [Header("Panels")]
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Button backButtonFromControls; // Кнопка "Назад" на панели управления

    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "GameScene"; // Укажи имя твоей игровой сцены

    private void Start()
    {
        // Убедимся, что панель управления скрыта при старте
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }

        // Назначаем слушателей на кнопки
        if (playButton != null) playButton.onClick.AddListener(PlayGame);
        if (controlsButton != null) controlsButton.onClick.AddListener(ShowControls);
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
        if (backButtonFromControls != null) backButtonFromControls.onClick.AddListener(HideControls);
        
        // Убедимся, что Time.timeScale = 1, если мы вышли из паузы игры
        Time.timeScale = 1f;

        // Плавное появление сцены при запуске
        if (ScreenFader.Instance != null)
        {
            StartCoroutine(ScreenFader.Instance.FadeIn());
        }
    }

    private void PlayGame()
    {
        // Используем ScreenFader для перехода
        if (ScreenFader.Instance != null)
        {
            StartCoroutine(FadeAndLoadScene(gameSceneName));
        }
        else
        {
            SceneManager.LoadScene(gameSceneName); // Обычная загрузка, если фейдера нет
        }
    }

    private void ShowControls()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(true);
            // Можно добавить анимацию появления панели
        }
    }

    private void HideControls()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
    }

    private void ExitGame()
    {
        Debug.Log("Выход из игры...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator FadeAndLoadScene(string sceneName)
    {
        yield return StartCoroutine(ScreenFader.Instance.FadeOut());
        SceneManager.LoadScene(sceneName);
        // FadeIn для новой сцены должен быть вызван в Start() менеджера той сцены
        // или здесь, если ScreenFader не будет обрабатывать это сам при загрузке новой сцены.
        // Но лучше, чтобы новая сцена сама делала FadeIn.
    }

    private void OnDestroy()
    {
        // Отписываемся от событий кнопок, чтобы избежать ошибок, если объект уничтожается
        if (playButton != null) playButton.onClick.RemoveListener(PlayGame);
        if (controlsButton != null) controlsButton.onClick.RemoveListener(ShowControls);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitGame);
        if (backButtonFromControls != null) backButtonFromControls.onClick.RemoveListener(HideControls);
    }
}