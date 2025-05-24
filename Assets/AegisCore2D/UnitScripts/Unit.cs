// Unit.cs
using System.Collections.Generic;
using AegisCore2D.GeneralScripts;
using AegisCore2D.UnitScripts;
using UnityEngine;

namespace AegisCore2D
{
    [RequireComponent(typeof(HealthComponent))] // Добавляем это
    public class Unit : MonoBehaviour, ISelectable
    {
        
        public GameObject GameObject { get; private set; }
        public bool Selected { get; private set; }
        public bool OutlineEnabled { get; private set; }
        
        // Свойство Team теперь будет делегировать к HealthComponent или устанавливать его
        [SerializeField] private int _teamIdInternal; // Для установки в инспекторе
        public int Team 
        { 
            get => Health?.TeamId ?? _teamIdInternal; // Возвращаем из HealthComponent, если есть
            set 
            {
                _teamIdInternal = value;
                if (Health != null)
                {
                    Health.SetTeamId(value);
                }
            }
        }

        [SerializeField] private UnitMove moveComponent;
        [SerializeField] private Outline outline;
        
        [Header("UI")]
        [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas;

        public UnitMove MoveComponent => moveComponent;
        public HealthComponent Health { get; private set; } // Добавляем ссылку на HealthComponent

        private readonly Queue<IUnitCommand> queue = new();
        
        private void Awake() // Изменяем Start на Awake для инициализации компонентов раньше
        {
            GameObject = gameObject;
            Health = GetComponent<HealthComponent>();
            
            if (Health != null)
            {
                Health.SetTeamId(_teamIdInternal);
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
                if(healthBarPrefab == null) Debug.LogWarning("Health Bar Prefab не назначен для юнита: " + gameObject.name, this);
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


        private void Start()
        {
            // Если _teamIdInternal был установлен в инспекторе, HealthComponent его уже должен был получить в своем Start или через наш Awake.
            // Либо здесь еще раз явно устанавливаем, если нужно:
            // if (Health != null) Health.SetTeamId(_teamIdInternal); 
            
            SelectionManager.RegisterUnitForTeam(this, Team);
        }

        private void HandleDeath(GameObject attacker)
        {
            // Здесь логика, специфичная для смерти юнита,
            // например, отмена текущих команд, проигрывание анимации смерти (когда будет)
            Debug.Log($"Unit {gameObject.name} is handling its death. Attacker: {attacker?.name}");
            queue.Clear(); // Очищаем очередь команд
            
            // Отписываемся от SelectionManager, если объект будет уничтожен HealthComponent'ом
            // Если юнит не уничтожается сразу (а, например, превращается в труп), то отписка не нужна здесь.
            // Но так как HealthComponent по умолчанию уничтожает GameObject, отписка здесь правильна.
             SelectionManager.RemoveUnitForTeam(this, Team); // Это уже есть в OnDestroy, но при смерти тоже нужно
            
            // Сам GameObject будет уничтожен в HealthComponent.Die()
            // Если нужно здесь дополнительно что-то сделать перед уничтожением (например, скрыть Outline)
            if (Selected) Deselect(); // Снять выделение, если был выделен
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) // Тестовая кнопка для нанесения урона
            {
                if (Selected) // Наносим урон только выделенным юнитам
                {
                    Health.TakeDamage(25, null); // Наносим 25 урона, без указания атакующего
                }
            }
            
            if (!Health.IsAlive || queue.Count == 0) return; // Не выполняем команды, если мертвы
            queue.Dequeue().Execute(this);
        }
        
        public void Enqueue(IUnitCommand cmd) 
        {
            if (!Health.IsAlive) return; // Не добавляем команды мертвым
            queue.Enqueue(cmd);
        }

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

        public void EnableOutline()  { if (Health.IsAlive) { outline.Show(true);  OutlineEnabled = true; } }
        public void DisableOutline() { outline.Show(false); OutlineEnabled = false; } // Можно не показывать аутлайн у мертвых, но снимать его всегда

        public void Select()
        {
            if (!Health.IsAlive) return; // Нельзя выделить мертвого
            EnableOutline();
            Selected = true;
            // Показ PathDisplay при выделении теперь в SelectionManager, это нормально
        }

        public void Deselect()
        {
            // Не важно, жив или мертв, снять выделение можно всегда
            DisableOutline();
            Selected = false;
            // Скрытие PathDisplay при снятии выделения теперь в SelectionManager
        }
    }
}