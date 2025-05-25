using System.Collections;
using AegisCore2D.GeneralScripts;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(HealthComponent), typeof(UnitMove), typeof(AttackComponent))]
    public class Unit : MonoBehaviour, ISelectable
    {
        public GameObject GameObject { get; private set; }
        public bool Selected { get; private set; }
        public bool OutlineEnabled { get; private set; }

        [SerializeField] private int teamIdInternal; // Serialized for editor assignment

        public int Team
        {
            get => Health?.TeamId ?? teamIdInternal; // Prioritize HealthComponent's team if available
            set
            {
                teamIdInternal = value;
                if (Health != null)
                {
                    Health.SetTeamId(value); // Keep HealthComponent's team in sync
                }
            }
        }

        [Header("Attack-Move Settings")]
        [SerializeField] private float attackMoveScanRadiusMultiplier = 1.5f;
        [SerializeField] private float minAttackMoveScanRadius = 3f;

        [Header("Component References")]
        [SerializeField] private UnitMove moveComponent;
        [SerializeField] private AttackComponent attackComponent;
        [SerializeField] private Outline outline; // Should be on a child object or this one

        [Header("UI")]
        [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas; // Shared canvas for all unit health bars
        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        private PathDisplay pathDisplay;
        private Animator animator;

        public UnitMove MoveComponent => moveComponent;
        public AttackComponent AttackComponent => attackComponent;
        public HealthComponent Health { get; private set; }
        public float AttackMoveScanRadiusMultiplier => attackMoveScanRadiusMultiplier;
        public float MinAttackMoveScanRadius => minAttackMoveScanRadius;

        private IUnitCommand currentCommand;

        private void Awake()
        {
            GameObject = gameObject; // Cache GameObject reference
            Health = GetComponent<HealthComponent>();
            // Other components are [SerializeField], Unity handles their assignment if dragged in Inspector
            // Fallback to GetComponent if not assigned in inspector (optional, good for robustness)
            if (moveComponent == null) moveComponent = GetComponent<UnitMove>();
            if (attackComponent == null) attackComponent = GetComponent<AttackComponent>();
            if (pathDisplay == null) pathDisplay = GetComponent<PathDisplay>(); // Optional
            if (outline == null) outline = GetComponentInChildren<Outline>(); // Optional, often a child

            if (Health != null)
            {
                Health.SetTeamId(teamIdInternal); // Ensure HealthComponent has the correct team ID from Unit
                Health.OnDeath += HandleDeath;
            }
            else Debug.LogError("Unit is missing HealthComponent!", this);

            if (moveComponent == null) Debug.LogError("Unit is missing UnitMove component!", this);
            if (attackComponent == null) Debug.LogError("Unit is missing AttackComponent!", this);
            // pathDisplay and outline logs are optional based on their necessity

            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"Юнит {gameObject.name} не имеет компонента Animator!", this);
            }
            
            SetupHealthBar();
        }
        
        private void SetupHealthBar()
        {
            if (worldSpaceCanvas == null)
            {
                // Find canvas once, by tag or name. Consider a more robust service locator for UI canvas.
                var canvasObj = GameObject.FindWithTag("HPBarWorldCanvas") ?? GameObject.Find("WorldSpaceUICanvas");
                if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                else Debug.LogError("Critical: WorldSpaceUICanvas (tagged 'HPBarWorldCanvas' or named 'WorldSpaceUICanvas') not found!");
            }

            if (healthBarPrefab != null && worldSpaceCanvas != null && Health != null)
            {
                var hbInstanceGo = Instantiate(healthBarPrefab, worldSpaceCanvas.transform);
                healthBarInstance = hbInstanceGo.GetComponent<HealthBarUI>();
                if (healthBarInstance != null)
                {
                    healthBarInstance.SetHealthComponent(Health);
                }
                else Debug.LogError("Health Bar Prefab missing HealthBarUI component!", healthBarPrefab);
            }
            
            if (animator != null && Health.IsAlive) // Управляем анимациями, только если аниматор есть и юнит жив
            {
                UpdateAnimations();
            }
            // else if (healthBarPrefab == null) Debug.LogWarning($"Health Bar Prefab not assigned for unit: {gameObject.name}", this); // Optional
        }


        private void LateUpdate() // For UI elements that track world objects
        {
            if (healthBarInstance != null && Health != null && healthBarInstance.gameObject.activeSelf && Health.IsAlive)
            {
                // Position health bar above unit; adjust Y offset as needed
                healthBarInstance.transform.position = transform.position + Vector3.up * 1.0f; // Example offset
            }
        }

        private void Start()
        {
            SelectionManager.RegisterUnitForTeam(this, Team);
            
            if (AttackComponent != null)
            {
                AttackComponent.OnAttackPerformed += HandleAttackPerformed; // Подписываемся
            }
        }
        
        private void HandleAttackPerformed(IDamageable target) // IDamageable может не использоваться здесь
        {
            TriggerShootAnimation();
        }
        
        private void UpdateAnimations()
        {
            // Анимация ходьбы
            if (moveComponent != null)
            {
                bool isCurrentlyMoving = moveComponent.IsMoving(); // IsMoving() из UnitMove.cs
                animator.SetBool(IsWalking, isCurrentlyMoving);
            }
            else
            {
                animator.SetBool(IsWalking, false); // Если нет компонента движения, считаем, что не идет
            }
        }
        
        public void TriggerShootAnimation()
        {
            if (animator != null && Health.IsAlive)
            {
                animator.SetTrigger(Shoot);
            }
        }

        private void HandleDeath(GameObject attacker) // GameObject attacker - кто нанес последний удар
        {
            // 1. Запускаем анимацию смерти
            if (animator != null)
            {
                animator.SetTrigger("Die"); // Используем триггер "Die"
                // Если бы мы хотели проиграть конкретный стейт без настроенных переходов:
                // animator.Play("Unit_Death"); // Это проиграет стейт с именем "Unit_Death" из нулевого слоя
            }

            // 2. Очищаем текущую команду, если была
            ClearCurrentCommand();

            // 3. Снимаем выделение, если был выделен
            if (Selected) Deselect();

            // 4. Сообщаем SelectionManager'у, что юнит удален (это уже должно быть в OnDestroy, но при смерти тоже можно, если объект не сразу уничтожается)
            // SelectionManager.RemoveUnitForTeam(this, Team); // Если объект будет жить некоторое время для анимации

            // 5. Деактивируем компоненты, которые могут мешать анимации смерти или продолжать логику
            if (moveComponent != null) moveComponent.Stop(); // Останавливаем движение
            if (AttackComponent != null) AttackComponent.enabled = false; // Отключаем возможность атаковать
            if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = false; // Отключаем коллайдер, чтобы не мешал
            // Отключаем AI, если есть
            var aiComponent = GetComponent<AegisCore2D.AI.BasicUnitAI>();
            if (aiComponent != null) aiComponent.enabled = false;


            // 6. Запускаем корутину для уничтожения объекта ПОСЛЕ проигрывания анимации
            StartCoroutine(DestroyAfterAnimation(GetAnimationLength("Unit_Death")));
        }
        
        private float GetAnimationLength(string animationName)
        {
            if (animator == null) return 0f;

            RuntimeAnimatorController ac = animator.runtimeAnimatorController;
            for (int i = 0; i < ac.animationClips.Length; i++)
            {
                if (ac.animationClips[i].name == animationName)
                {
                    return ac.animationClips[i].length / animator.speed; // Учитываем скорость аниматора
                }
            }
            Debug.LogWarning($"Длина анимации '{animationName}' не найдена.");
            return 1f; // Возвращаем значение по умолчанию, если не нашли
        }

        private IEnumerator DestroyAfterAnimation(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Убедимся, что объект еще существует (на всякий случай)
            if (this != null && gameObject != null)
            {
                // Отписка от SelectionManager происходит в OnDestroy, который вызовется при Destroy(gameObject)
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Health == null || !Health.IsAlive)
            {
                if (currentCommand != null) ClearCurrentCommand(); // Ensure command is cleared if unit died externally
                return;
            }

            ExecuteCurrentCommand();
            if (animator != null && Health.IsAlive) // Управляем анимациями, только если аниматор есть и юнит жив
            {
                UpdateAnimations();
            }
        }

        private void ExecuteCurrentCommand()
        {
            if (currentCommand == null) return;

            if (currentCommand is AttackCommand attackCmd)
            {
                if (!attackCmd.IsTargetStillValid()) ClearCurrentCommand();
                else currentCommand.Execute(this);
            }
            else if (currentCommand is MoveCommand) // No AttackMoveCommand check needed here if it transitions
            {
                currentCommand.Execute(this);
                if (MoveComponent != null && MoveComponent.HasReachedDestination())
                {
                    ClearCurrentCommand();
                }
            }
            else // Includes AttackMoveCommand and any other commands
            {
                 currentCommand.Execute(this);
            }
        }
        
        private void UpdatePathDisplayForCurrentCommand()
        {
            if (pathDisplay == null || !pathDisplay.isActiveAndEnabled) return;

            if (!Selected || Health == null || !Health.IsAlive) // Don't show path if not selected or dead
            {
                pathDisplay.SetVisible(false);
                return;
            }
            pathDisplay.SetVisible(true); // Make visible if selected and alive

            if (currentCommand is AttackCommand attackCmd && attackCmd.IsTargetStillValid())
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.Attack);
                pathDisplay.SetAttackTargetOverride(attackCmd.GetTarget());
            }
            else if (currentCommand is AttackMoveCommand) // AttackMove has a target position, not a specific entity target for path
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.AttackMove);
                pathDisplay.SetAttackTargetOverride(null); 
            }
            else if (currentCommand is MoveCommand)
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.Default);
                pathDisplay.SetAttackTargetOverride(null);
            }
            else // No command or unknown command type
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.None);
                pathDisplay.SetAttackTargetOverride(null);
            }
        }

        public void SetCommand(IUnitCommand cmd)
        {
            if (Health == null || !Health.IsAlive) return;

            // Basic optimization: avoid re-assigning identical commands if not strictly necessary
            if (currentCommand?.GetType() == cmd?.GetType())
            {
                // Add more specific checks if needed (e.g. target comparison)
                if (currentCommand is MoveCommand oldMove && cmd is MoveCommand newMove &&
                    Vector3.Distance(oldMove.GetTargetPosition_DEBUG(), newMove.GetTargetPosition_DEBUG()) < 0.1f &&
                    (MoveComponent != null && MoveComponent.IsMoving())) // Only skip if already moving to same spot
                {
                    return;
                }
                // Similar checks for AttackCommand, AttackMoveCommand target/position
            }

            currentCommand = cmd;
            UpdatePathDisplayForCurrentCommand(); // Update path display when command changes
        }

        public void ClearCurrentCommand()
        {
            if (currentCommand is AttackCommand || currentCommand is AttackMoveCommand) // If it was an offensive command
            {
                if (MoveComponent != null && MoveComponent.IsMoving())
                {
                     // Decide if unit should stop or continue to last move point of AttackMove
                     // For now, just stop if it was an attack command that got cleared.
                     if (currentCommand is AttackCommand) MoveComponent.Stop();
                }
            }
            currentCommand = null;
            UpdatePathDisplayForCurrentCommand();
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.OnDeath -= HandleDeath; // Unsubscribe
            }
            // SelectionManager.RemoveUnitForTeam is called in HandleDeath if unit is destroyed by self.
            // If destroyed externally, this OnDestroy ensures cleanup.
            // However, if HandleDeath already called Destroy(gameObject), this might be redundant or run on already destroyed obj.
            // It's safer if HandleDeath calls RemoveUnitForTeam, and external destruction also calls it or relies on this.
            // For robustness:
            if(Health == null || Health.IsAlive) // If not already handled by HandleDeath (i.e. unit destroyed externally while alive)
            {
                 SelectionManager.RemoveUnitForTeam(this, Team);
            }
            
            if (AttackComponent != null)
            {
                AttackComponent.OnAttackPerformed -= HandleAttackPerformed;
            }
        }

        // ISelectable Implementation
        public void EnableOutline()
        {
            if (outline != null && Health != null && Health.IsAlive)
            {
                outline.Show(true);
                OutlineEnabled = true;
            }
        }

        public void DisableOutline()
        {
            if (outline != null)
            {
                outline.Show(false);
                OutlineEnabled = false;
            }
        }

        public void Select()
        {
            if (Health == null || !Health.IsAlive) return;
            EnableOutline(); // Unit's primary selection outline
            Selected = true;
            UpdatePathDisplayForCurrentCommand(); // Show path when selected
        }

        public void Deselect()
        {
            DisableOutline();
            Selected = false;
            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(false); // Hide path when deselected
            }
        }
        
        // For debugging purposes
        public IUnitCommand GetCurrentCommand_DEBUG()
        {
            return currentCommand;
        }
    }
}