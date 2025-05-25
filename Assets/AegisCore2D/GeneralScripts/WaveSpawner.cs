using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Добавлено для Any()
using UnityEngine.UI;
using TMPro;
using AegisCore2D.UnitScripts;
using AegisCore2D.AI;

namespace AegisCore2D.GameManagement
{
    public class WaveSpawner : MonoBehaviour
    {
        public enum GameState { Pregame, WaitingForNextWave, Spawning, WaveInProgress, Victory, Defeat }

        [Header("Wave Configuration")]
        [SerializeField] private List<WaveDefinition> waves = new List<WaveDefinition>();
        [Tooltip("Точки спауна по умолчанию. WaveDefinition может их переопределить.")]
        [SerializeField] private List<Transform> defaultSpawnPoints = new List<Transform>();
        [Tooltip("Цель, к которой будут идти заспавненные враги (например, база игрока).")]
        [SerializeField] private Transform enemyInitialGoalTarget;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI waveCounterText;
        [SerializeField] private TextMeshProUGUI enemiesRemainingText;
        [SerializeField] private TextMeshProUGUI nextWaveTimerText;
        [SerializeField] private TextMeshProUGUI gameStatusText;
        [SerializeField] private GameObject spawnIndicatorPrefab;

        [Header("Game State")]
        [Tooltip("Задержка (в секундах) перед самой первой волной. Будет использована как 'delayBeforeWave' для фиктивной \"нулевой\" волны.")]
        [SerializeField] private float initialDelayBeforeFirstWave = 5f;

        private int currentWaveIndex = -1;
        private List<Unit> activeEnemies = new List<Unit>();
        private GameState currentGameState = GameState.Pregame;
        private float countdownToNextWave;
        private GameObject currentSpawnIndicatorInstance;

        public static event System.Action OnPlayerVictory;
        public static event System.Action OnPlayerDefeat;
        public static event System.Action<int, int> OnWaveStatusChanged; // currentWaveNumber (1-based), totalWaves
        public static event System.Action<int> OnEnemiesRemainingChanged;

        // Синглтон для легкого доступа, если понадобится из других скриптов (например, для вызова TriggerPlayerDefeat)
        public static WaveSpawner Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject); // Раскомментируй, если WaveSpawner должен выживать при смене сцен
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }


        private void Start()
        {
            if (waves.Count == 0)
            {
                Debug.LogError("Нет определенных волн в WaveSpawner!", this);
                enabled = false;
                return;
            }
            if (defaultSpawnPoints.Count == 0 && waves.Any(w => w.spawnPointOverride == null))
            {
                Debug.LogError("Нет точек спауна по умолчанию, а некоторые волны их не переопределяют!", this);
                enabled = false;
                return;
            }
            if (enemyInitialGoalTarget == null)
            {
                 Debug.LogWarning("EnemyInitialGoalTarget не назначен в WaveSpawner. Враги будут использовать свою стартовую позицию как цель.", this);
            }

            currentGameState = GameState.Pregame; // Устанавливаем начальное состояние
            StartCoroutine(GameLoop());
        }

        private void Update()
        {
            UpdateUI();
            CheckPlayerDefeatCondition();
        }

        private IEnumerator GameLoop()
        {
            // Цикл по всем волнам
            for (int i = 0; i < waves.Count; i++)
            {
                if (currentGameState == GameState.Defeat || currentGameState == GameState.Victory) yield break;

                currentWaveIndex = i;
                WaveDefinition currentWave = waves[currentWaveIndex];
                OnWaveStatusChanged?.Invoke(currentWaveIndex + 1, waves.Count);

                // Определяем задержку: для первой волны используем initialDelay, для последующих - из WaveDefinition
                float delay = (i == 0) ? initialDelayBeforeFirstWave : currentWave.delayBeforeWave;

                // Показываем объявление и индикатор спауна
                if (gameStatusText != null && !string.IsNullOrEmpty(currentWave.waveAnnouncement))
                {
                    // Для первой волны можно кастомизировать сообщение, если нужно
                    var announcement = (i == 0) ? $"Игра начинается! {currentWave.waveAnnouncement}" : currentWave.waveAnnouncement;
                    gameStatusText.text = announcement;
                    gameStatusText.gameObject.SetActive(true);
                }
                ShowSpawnIndicator(currentWave); // Показываем индикатор для текущей (предстоящей) волны

                countdownToNextWave = delay;
                currentGameState = GameState.WaitingForNextWave;

                while (countdownToNextWave > 0)
                {
                    if (currentGameState == GameState.Defeat || currentGameState == GameState.Victory) yield break;
                    countdownToNextWave -= Time.deltaTime;
                    yield return null;
                }

                if (currentGameState == GameState.Defeat || currentGameState == GameState.Victory) yield break;

                if (gameStatusText != null) gameStatusText.gameObject.SetActive(false);
                HideSpawnIndicator();

                yield return StartCoroutine(SpawnWave(currentWave));
                currentGameState = GameState.WaveInProgress;

                while (activeEnemies.Count > 0)
                {
                    if (currentGameState == GameState.Defeat || currentGameState == GameState.Victory) yield break;
                    activeEnemies.RemoveAll(enemy => enemy == null || (enemy.Health != null && !enemy.Health.IsAlive));
                    OnEnemiesRemainingChanged?.Invoke(activeEnemies.Count);
                    yield return new WaitForSeconds(0.5f);
                }
                OnEnemiesRemainingChanged?.Invoke(0); // Убедимся, что счетчик обнулился

                if (currentGameState == GameState.Defeat || currentGameState == GameState.Victory) yield break;
            }

            // Если цикл завершился и не было поражения/победы (что маловероятно, но для полноты)
            if (currentGameState != GameState.Defeat && currentGameState != GameState.Victory)
            {
                HandlePlayerVictory();
            }
        }


        private IEnumerator SpawnWave(WaveDefinition wave)
        {
            currentGameState = GameState.Spawning;
            // Debug.Log($"Начинается волна {currentWaveIndex + 1}. Врагов: {wave.enemyCount}");

            Transform spawnPointToUse = wave.spawnPointOverride != null ? wave.spawnPointOverride : GetRandomDefaultSpawnPoint();
            if (spawnPointToUse == null)
            {
                Debug.LogError($"Не удалось определить точку спауна для волны {currentWaveIndex + 1}!");
                yield break;
            }

            for (int i = 0; i < wave.enemyCount; i++)
            {
                if (currentGameState == GameState.Defeat || currentGameState == GameState.Victory) yield break;
                SpawnEnemy(wave.enemyPrefab, spawnPointToUse);
                yield return new WaitForSeconds(wave.spawnInterval);
            }
        }

        private void SpawnEnemy(GameObject enemyPrefab, Transform spawnPoint)
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("Попытка заспаунить врага с пустым префабом!");
                return;
            }
            GameObject enemyGO = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
            Unit enemyUnit = enemyGO.GetComponent<Unit>();

            if (enemyUnit != null)
            {
                activeEnemies.Add(enemyUnit);
                OnEnemiesRemainingChanged?.Invoke(activeEnemies.Count);

                BasicUnitAI ai = enemyGO.GetComponent<BasicUnitAI>();
                if (ai != null && enemyInitialGoalTarget != null)
                {
                    ai.SetInitialGoalPosition(enemyInitialGoalTarget.position);
                }
            }
            else
            {
                Debug.LogError($"Префаб {enemyPrefab.name} не содержит компонент Unit!", enemyPrefab);
                Destroy(enemyGO);
            }
        }

        private Transform GetRandomDefaultSpawnPoint()
        {
            if (defaultSpawnPoints.Count == 0) return null;
            return defaultSpawnPoints[Random.Range(0, defaultSpawnPoints.Count)];
        }

        private void ShowSpawnIndicator(WaveDefinition wave)
        {
            if (spawnIndicatorPrefab == null) return;

            Transform spawnPointToIndicate = wave.spawnPointOverride;
            if (spawnPointToIndicate == null) // Если в волне не указана точка, берем случайную из дефолтных
            {
                spawnPointToIndicate = GetRandomDefaultSpawnPoint();
            }
            // Если и случайной нет (хотя мы проверяли в Start), но на всякий случай
            if (spawnPointToIndicate == null && defaultSpawnPoints.Count > 0)
            {
                 spawnPointToIndicate = defaultSpawnPoints[0];
            }


            if (spawnPointToIndicate != null)
            {
                if (currentSpawnIndicatorInstance != null) Destroy(currentSpawnIndicatorInstance);
                currentSpawnIndicatorInstance = Instantiate(spawnIndicatorPrefab, spawnPointToIndicate.position + Vector3.up * 0.5f, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning($"Не удалось определить точку для индикатора спауна волны {currentWaveIndex + 1}.");
            }
        }

        private void HideSpawnIndicator()
        {
            if (currentSpawnIndicatorInstance != null)
            {
                Destroy(currentSpawnIndicatorInstance);
                currentSpawnIndicatorInstance = null;
            }
        }

        private void UpdateUI()
        {
            // Обновляем счетчик волн: если Pregame, показываем 0 или 1 из N, в зависимости от предпочтений
            if (waveCounterText != null)
            {
                int displayWaveNumber = (currentGameState == GameState.Pregame || currentWaveIndex == -1) ? 1 : currentWaveIndex + 1;
                 // Если игра закончилась, показываем последнюю достигнутую или максимальную
                if (currentGameState == GameState.Victory) displayWaveNumber = waves.Count;
                else if (currentGameState == GameState.Defeat && currentWaveIndex >= 0) displayWaveNumber = currentWaveIndex + 1;
                else if (currentGameState == GameState.Defeat && currentWaveIndex == -1) displayWaveNumber = 1; // Если проиграли до первой волны

                waveCounterText.text = $"Волна: {displayWaveNumber} / {waves.Count}";
            }

            if (enemiesRemainingText != null)
            {
                enemiesRemainingText.text = $"Врагов осталось: {activeEnemies.Count}";
            }

            if (nextWaveTimerText != null)
            {
                // Показываем таймер, если ждем следующую волну (включая Pregame, если initialDelay используется)
                if ((currentGameState == GameState.WaitingForNextWave || 
                    (currentGameState == GameState.Pregame && waves.Count > 0)) // Добавил Pregame для первой волны
                    && countdownToNextWave > 0)
                {
                    nextWaveTimerText.gameObject.SetActive(true);
                    nextWaveTimerText.text = $"Следующая волна через: {Mathf.CeilToInt(countdownToNextWave)}с";
                }
                else
                {
                    nextWaveTimerText.gameObject.SetActive(false);
                }
            }
        }


        private void HandlePlayerVictory()
        {
            if (currentGameState == GameState.Victory || currentGameState == GameState.Defeat) return;
            
            currentGameState = GameState.Victory;
            if (gameStatusText != null)
            {
                gameStatusText.text = "ПОБЕДА!";
                gameStatusText.gameObject.SetActive(true);
            }
            HideSpawnIndicator();
            OnPlayerVictory?.Invoke();
            Time.timeScale = 0f;
        }

        private void HandlePlayerDefeat()
        {
            if (currentGameState == GameState.Victory || currentGameState == GameState.Defeat) return;

            currentGameState = GameState.Defeat;
            if (gameStatusText != null)
            {
                gameStatusText.text = "ПОРАЖЕНИЕ!";
                gameStatusText.gameObject.SetActive(true);
            }
            HideSpawnIndicator();
            OnPlayerDefeat?.Invoke();
            Time.timeScale = 0f;
        }

        private int CountPlayerUnits()
        {
            int count = 0;
            Unit[] allUnits = FindObjectsOfType<Unit>();
            foreach (var unit in allUnits)
            {
                if (unit.Team == 0 && unit.Health != null && unit.Health.IsAlive)
                {
                    count++;
                }
            }
            return count;
        }

        private void CheckPlayerDefeatCondition()
        {
            if (currentGameState == GameState.Victory || currentGameState == GameState.Defeat) return;

            // Не проверять до тех пор, пока не начнется фактический игровой процесс (например, первая волна заспаунилась или вот-вот начнется)
            // currentWaveIndex становится >= 0 когда мы входим в цикл обработки первой волны.
            // Можно добавить проверку, что это не Pregame, если initialDelay очень долгий.
            if (currentWaveIndex >= 0 || currentGameState == GameState.WaitingForNextWave)
            {
                 if (CountPlayerUnits() == 0)
                 {
                    HandlePlayerDefeat();
                 }
            }
        }

        public void TriggerPlayerDefeat_BaseDestroyed()
        {
            HandlePlayerDefeat();
        }
    }
}