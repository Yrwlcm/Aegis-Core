// Unit.cs

using System.Collections.Generic;
using AegisCore2D.GeneralScripts;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(HealthComponent), typeof(UnitMove), typeof(AttackComponent))] // Добавляем это
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
        [SerializeField] private AttackComponent attackComponent;
        [SerializeField] private Outline outline;

        [Header("UI")] [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas;
        private PathDisplay pathDisplay;

        public UnitMove MoveComponent => moveComponent;
        public AttackComponent AttackComponent => attackComponent;
        public HealthComponent Health { get; private set; } // Добавляем ссылку на HealthComponent


        // Заменяем Queue на одну текущую команду для упрощения логики "атакуй-иди"
        // private readonly Queue<IUnitCommand> commandQueue = new();
        private IUnitCommand currentCommand;

        private void Awake() // Изменяем Start на Awake для инициализации компонентов раньше
        {
            GameObject = gameObject;
            Health = GetComponent<HealthComponent>();
            pathDisplay = GetComponent<PathDisplay>();

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
            ClearCurrentCommand(); // Очищаем очередь команд

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
            if (!Health.IsAlive)
            {
                // Если юнит умер, а команда была, очистим ее
                if (currentCommand != null) ClearCurrentCommand();
                return;
            }

            if (currentCommand != null)
            {
                // Проверяем, актуальна ли команда (например, если это AttackCommand, жива ли цель)
                if (currentCommand is AttackCommand attackCmd) // Используем "is" с объявлением переменной
                {
                    if (!attackCmd.IsTargetStillValid())
                    {
                        //Debug.Log($"{name}: Цель AttackCommand более не валидна, очищаем команду.");
                        ClearCurrentCommand(); // Цель умерла или исчезла
                    }
                    else
                    {
                        currentCommand.Execute(this);
                    }
                }
                else // Для других типов команд (например, MoveCommand)
                {
                    currentCommand.Execute(this);
                    // MoveCommand должен сам решить, когда он выполнен (например, по достижению цели)
                    // и вызвать ClearCurrentCommand() у юнита.
                    // Пока MoveCommand этого не делает, он будет выполняться каждый кадр.
                    if (currentCommand is MoveCommand && MoveComponent.HasReachedDestination())
                    {
                        //Debug.Log($"{name}: Достиг цели MoveCommand, очищаем команду.");
                        ClearCurrentCommand();
                    }
                }
            }
        }

        /// <summary>
        /// Назначает новую команду юниту, отменяя предыдущую.
        /// </summary>
        public void SetCommand(IUnitCommand cmd)
        {
            if (!Health.IsAlive) return;

            // Если это команда движения, и новая команда тоже движения к той же точке, можно не прерывать
            if (currentCommand is MoveCommand oldMoveCmd && cmd is MoveCommand newMoveCmd)
            {
                if (Vector3.Distance(MoveComponent.GetDestination(), ((MoveCommand)cmd).GetTargetPosition_DEBUG()) <
                    0.1f) // GetTargetPosition_DEBUG нужно будет добавить в MoveCommand
                {
                    // Уже движемся туда же
                    return;
                }
            }


            // Если текущая команда - атака, и новая команда - атака той же цели, не прерываем
            if (currentCommand is AttackCommand oldAttackCmd && cmd is AttackCommand newAttackCmd)
            {
                if (oldAttackCmd.GetTarget() == newAttackCmd.GetTarget())
                {
                    return; // Уже атакуем эту цель
                }
            }

            // Перед назначением новой команды, если юнит двигался по старой, остановим его.
            // Это спорный момент: иногда мы хотим, чтобы юнит "переключился" на лету.
            // if (MoveComponent.IsMoving())
            // {
            //    MoveComponent.Stop();
            // }

            currentCommand = cmd;
            //Debug.Log($"{name} получил новую команду: {cmd.GetType().Name}");

            // Обновляем PathDisplay
            if (pathDisplay != null && pathDisplay.isActiveAndEnabled &&
                Selected) // Обновляем только если выбран и активен
            {
                if (cmd is AttackCommand)
                {
                    pathDisplay.SetPathMode(true); // Режим атаки
                }
                else if (cmd is MoveCommand)
                {
                    pathDisplay.SetPathMode(false); // Режим движения
                }
                // Если команда null (очищена), PathDisplay должен сам сбросить цвет или сделать это в ClearCurrentCommand
            }
        }

        // Метод для команды, чтобы сообщить юниту о своем завершении
        public void ClearCurrentCommand()
        {
            //Debug.Log($"{name}: Команда {currentCommand?.GetType().Name} завершена/очищена.");
            if (currentCommand != null && currentCommand is AttackCommand)
            {
                // Если это была атака, и мы очищаем команду (например, цель умерла),
                // то нужно остановить движение, если юнит двигался к цели.
                if (MoveComponent.IsMoving())
                {
                    MoveComponent.Stop();
                }
            }

            currentCommand = null;

            // Сбрасываем цвет пути на дефолтный, если команда очищена
            if (pathDisplay != null && pathDisplay.isActiveAndEnabled && Selected)
            {
                pathDisplay.SetPathMode(false); // Сброс на режим движения (дефолтный)
            }
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

        public void EnableOutline()
        {
            if (Health.IsAlive)
            {
                outline.Show(true);
                OutlineEnabled = true;
            }
        }

        public void DisableOutline()
        {
            outline.Show(false);
            OutlineEnabled = false;
        } // Можно не показывать аутлайн у мертвых, но снимать его всегда

        public void Select()
        {
            if (!Health.IsAlive) return;
            EnableOutline();
            Selected = true;

            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(true);
                // Устанавливаем цвет в зависимости от текущей команды
                if (currentCommand is AttackCommand) pathDisplay.SetPathMode(true);
                else pathDisplay.SetPathMode(false); // Включая случай, когда currentCommand == null
            }
        }

        public void Deselect()
        {
            DisableOutline();
            Selected = false;
            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(false);
                // pathDisplay.SetPathMode(false); // Можно сбросить цвет при деселекте
            }
        }
    }
}