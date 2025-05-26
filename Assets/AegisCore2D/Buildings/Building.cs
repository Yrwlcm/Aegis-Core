using AegisCore2D.GeneralScripts;
using AegisCore2D.UnitScripts;
using UnityEngine;

namespace AegisCore2D.Buildings
{
    [RequireComponent(typeof(HealthComponent))] // Добавляем это
    public abstract class Building : MonoBehaviour
    {
        public GameObject GameObject { get; private set; }

        [Header("UI")] [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas;
        public HealthComponent Health { get; private set; } // Добавляем ссылку на HealthComponent

        private void Awake() // Изменяем Start на Awake для инициализации компонентов раньше
        {
            GameObject = gameObject;
            Health = GetComponent<HealthComponent>();

            if (Health != null)
            {
                Health.OnDeath += HandleDeath;
            }

            if (worldSpaceCanvas == null)
            {
                GameObject canvasObj = GameObject.FindWithTag("HPBarWorldCanvas");
                if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                if (worldSpaceCanvas == null)
                {
                    canvasObj = GameObject.Find("WorldSpaceUICanvas");
                    if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                }

                if (worldSpaceCanvas == null)
                {
                    Debug.LogError("WorldSpaceUICanvas не найден в сцене!");
                }
            }

            if (healthBarPrefab != null && worldSpaceCanvas != null)
            {
                GameObject hbInstanceGo = Instantiate(healthBarPrefab, worldSpaceCanvas.transform);
                healthBarInstance = hbInstanceGo.GetComponent<HealthBarUI>();

                if (healthBarInstance != null)
                {
                    healthBarInstance.SetHealthComponent(this.Health);
                    // Начальное позиционирование HP бара будет в центре юнита.
                    // Обновление позиции - в LateUpdate.
                }
                else
                {
                    Debug.LogError("Префаб HP бара не содержит HealthBarUI компонент!", this);
                }
            }
            else
            {
                if (healthBarPrefab == null)
                    Debug.LogWarning("Health Bar Prefab не назначен для юнита: " + gameObject.name, this);
            }
        }

        void LateUpdate() // Изменяем Unit.LateUpdate
        {
            if (healthBarInstance != null && healthBarInstance.gameObject.activeSelf && Health.IsAlive)
            {
                // Позиционируем HP бар в центре трансформа юнита
                healthBarInstance.transform.position = transform.position;

                // Billboard эффект (поворот к камере) уже обрабатывается в HealthBarUI.LateUpdate()
            }
            // Скрытие бара при смерти юнита также обрабатывается в HealthBarUI через событие OnDeath
            // или здесь, если нужно гарантировать:
            else if (healthBarInstance != null && !Health.IsAlive && healthBarInstance.gameObject.activeSelf)
            {
                healthBarInstance.gameObject.SetActive(false);
            }
        }

        protected abstract void HandleDeath(GameObject attacker);

        private void OnDestroy()
        {
            // Deselect() здесь может вызвать ошибку, если GameObject уже уничтожается
            // Лучше логику деселекта и отписки от менеджера перенести в HandleDeath или гарантировать,
            // что OnDestroy вызывается до фактического уничтожения связанных систем.
            // Однако, SelectionManager.RemoveUnitForTeam должен быть устойчив к тому, что юнит уже удален.

            // Если подписывались на события, здесь отписываемся
            if (Health != null)
            {
                Health.OnDeath -= HandleDeath;
            }
            // Важно: если юнит умирает и уничтожается, SelectionManager.RemoveUnitForTeam
            // должен быть вызван ДО того, как ссылка на этот Unit станет невалидной в SelectionManager.
            // Вызов в HandleDeath уже решает эту проблему.
            // Но если юнит уничтожается по другой причине (не смерть), то OnDestroy важен.
            // SelectionManager.RemoveUnitForTeam(this, Team); // Этот вызов уже есть в HandleDeath. Если оставить и тут, убедись, что это безопасно (не вызовет ошибок при двойном удалении)
        }
    }
}