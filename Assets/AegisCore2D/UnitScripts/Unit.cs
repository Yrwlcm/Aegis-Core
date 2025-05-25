using System.Collections;
using System.Linq;
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

        [SerializeField] private int teamIdInternal;

        public int Team
        {
            get => Health?.TeamId ?? teamIdInternal;
            set
            {
                teamIdInternal = value;
                if (Health != null)
                {
                    Health.SetTeamId(value);
                }
            }
        }

        [Header("Attack-Move Settings")] [SerializeField]
        private float attackMoveScanRadiusMultiplier = 1.5f;

        [SerializeField] private float minAttackMoveScanRadius = 3f;

        [Header("Auto-Aggression Settings")]
        [Tooltip("Включить автоматическую атаку ближайших врагов, если юнит бездействует или на позиции.")]
        [SerializeField]
        private bool enableAutoAggression = true;

        [Tooltip("Радиус сканирования для автоматической атаки. Если 0, используется MinAttackMoveScanRadius.")]
        [SerializeField]
        private float autoAggroScanRadius = 0f;

        [Tooltip("Как часто (в секундах) юнит сканирует в поисках врагов для авто-атаки.")] [SerializeField]
        private float autoAggroScanInterval = 1.0f;


        [Header("Component References")] [SerializeField]
        private UnitMove moveComponent;

        [SerializeField] private AttackComponent attackComponent;
        [SerializeField] private Outline outline;
        private SpriteRenderer unitSpriteRenderer;
        private SpriteRenderer unitOutlineSpriteRenderer;

        [Header("UI")] [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas;

        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        private static readonly int Die = Animator.StringToHash("Die");
        private static readonly int Melee = Animator.StringToHash("Melee");
        private static readonly int Explode = Animator.StringToHash("Explode");

        private PathDisplay pathDisplay;
        private Animator animator;

        [Header("Effects")] [SerializeField] private Animator explosionEffectAnimator;

        public UnitMove MoveComponent => moveComponent;
        public AttackComponent AttackComponent => attackComponent;
        public HealthComponent Health { get; private set; }
        public float AttackMoveScanRadiusMultiplier => attackMoveScanRadiusMultiplier;
        public float MinAttackMoveScanRadius => minAttackMoveScanRadius;

        private IUnitCommand currentCommand;
        private float lastAutoAggroScanTime;

        private void Awake()
        {
            GameObject = gameObject;
            Health = GetComponent<HealthComponent>();

            if (moveComponent == null) moveComponent = GetComponent<UnitMove>();
            if (attackComponent == null) attackComponent = GetComponent<AttackComponent>();
            if (pathDisplay == null) pathDisplay = GetComponent<PathDisplay>();
            if (outline == null) outline = GetComponentInChildren<Outline>();


            unitSpriteRenderer = GetComponent<SpriteRenderer>();
            unitOutlineSpriteRenderer = transform.Find("Outline").GetComponent<SpriteRenderer>();
            if (unitSpriteRenderer == null)
            {
                Debug.LogWarning($"Юнит {gameObject.name} не имеет компонента SpriteRenderer на основном объекте для разворота!", this);
            }

            if (Health != null)
            {
                Health.SetTeamId(teamIdInternal);
                Health.OnDeath += HandleDeath;
            }
            else Debug.LogError("Unit is missing HealthComponent!", this);

            if (moveComponent == null) Debug.LogError("Unit is missing UnitMove component!", this);
            if (attackComponent == null) Debug.LogError("Unit is missing AttackComponent!", this);

            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"Юнит {gameObject.name} не имеет компонента Animator!", this);
            }

            if (enableAutoAggression && autoAggroScanRadius <= 0.01f)
            {
                autoAggroScanRadius = minAttackMoveScanRadius;
            }

            SetupHealthBar();
        }

        private void SetupHealthBar()
        {
            if (worldSpaceCanvas == null)
            {
                var canvasObj = GameObject.FindWithTag("HPBarWorldCanvas") ?? GameObject.Find("WorldSpaceUICanvas");
                if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                else
                    Debug.LogError(
                        "Critical: WorldSpaceUICanvas (tagged 'HPBarWorldCanvas' or named 'WorldSpaceUICanvas') not found!");
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
        }

        private void LateUpdate()
        {
            if (healthBarInstance != null && Health != null && healthBarInstance.gameObject.activeSelf &&
                Health.IsAlive)
            {
                healthBarInstance.transform.position = transform.position + Vector3.up * 1.0f;
            }
        }

        private void Start()
        {
            SelectionManager.RegisterUnitForTeam(this, Team);

            if (AttackComponent != null)
            {
                AttackComponent.OnAttackPerformed += HandleAttackPerformed;
            }
        }

        private void HandleAttackPerformed(IDamageable target)
        {
            if (animator == null) return; // Нет аниматора - нет анимаций

            if (attackComponent.IsRanged)
                TriggerShootAnimation();
            else
                TriggerMeleeAnimation();
        }
        
        private void UpdateAnimations()
        {
            if (animator == null && unitSpriteRenderer == null) return; // Nothing to update

            if (moveComponent != null && Health != null && Health.IsAlive)
            {
                // Animation state
                if (animator != null)
                {
                    bool isCurrentlyMoving = moveComponent.IsMoving();
                    animator.SetBool(IsWalking, isCurrentlyMoving);
                }

                // Sprite flipping logic
                if (unitSpriteRenderer != null && moveComponent.agent != null)
                {
                    float horizontalVelocity = moveComponent.agent.desiredVelocity.x;
                    // A small threshold to prevent flipping when standing still or moving very slowly vertically
                    float flipThreshold = 0.05f;

                    if (horizontalVelocity < -flipThreshold)
                    {
                        unitSpriteRenderer.flipX = true; // Moving left
                        unitOutlineSpriteRenderer.flipX = true;
                    }
                    else if (horizontalVelocity > flipThreshold)
                    {
                        unitSpriteRenderer.flipX = false; // Moving right
                        unitOutlineSpriteRenderer.flipX = false;
                    }
                    // If horizontalVelocity is between -flipThreshold and flipThreshold (e.g. moving vertically or stopped),
                    // the sprite maintains its current flipX state.
                }
            }
            else
            {
                if (animator != null)
                {
                    animator.SetBool(IsWalking, false);
                }
            }
        }


        public void TriggerShootAnimation()
        {
            if (animator != null && Health.IsAlive)
            {
                animator.SetTrigger(Shoot);
            }
        }

        public void TriggerMeleeAnimation()
        {
            if (animator != null && Health.IsAlive)
            {
                animator.SetTrigger(Melee);
            }
        }

        private void HandleDeath(GameObject attacker) // GameObject attacker - кто нанес последний удар
        {
            // 1. Запускаем анимацию смерти
            if (animator != null)
            {
                animator.SetTrigger(Die);
                
            }
            if (explosionEffectAnimator != null)
            {
                explosionEffectAnimator.SetTrigger(Explode);
            }

            // 2. Очищаем текущую команду, если была
            ClearCurrentCommand();

            // 3. Снимаем выделение, если был выделен
            if (Selected) Deselect();

            // 4. Сообщаем SelectionManager'у, что юнит удален (это уже должно быть в OnDestroy, но при смерти тоже можно, если объект не сразу уничтожается)
            // SelectionManager.RemoveUnitForTeam(this, Team); // Если объект будет жить некоторое время для анимации

            // 5. Деактивируем компоненты, которые могут мешать анимации смерти или продолжать логику
            if (moveComponent != null) moveComponent.Stop();
            if (AttackComponent != null) AttackComponent.enabled = false;
            if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = false;
            var aiComponent = GetComponent<AegisCore2D.AI.BasicUnitAI>();
            if (aiComponent != null) aiComponent.enabled = false;


            var deathEffectDuration = 0f;
            if (explosionEffectAnimator != null)
            {
                deathEffectDuration = GetAnimationLength(explosionEffectAnimator,"Explosion");
            }
            else
            {
                var deathAnimation = GetAnimationLength(animator,"Death");
                if (deathAnimation > deathEffectDuration)
                    deathEffectDuration = deathAnimation;
            }
            
    
            StartCoroutine(DestroyAfterDelay(deathEffectDuration));
        }


        private float GetAnimationLength(Animator targetAnimator, string animationName)
        {
            if (targetAnimator == null) return 0f;

            RuntimeAnimatorController ac = targetAnimator.runtimeAnimatorController;
            if (ac == null) return 0f;

            foreach (var clip in ac.animationClips)
            {
                if (clip.name == animationName)
                {
                    return clip.length / targetAnimator.speed;
                }
            }

            // Debug.LogWarning($"Длина анимации '{animationName}' для аниматора {targetAnimator.gameObject.name} не найдена.");
            return 0f;
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Health == null || !Health.IsAlive)
            {
                if (currentCommand != null) ClearCurrentCommand();
                return;
            }

            ExecuteCurrentCommand();

            if (enableAutoAggression && CanConsiderAutoAggression())
            {
                if (Time.time > lastAutoAggroScanTime + autoAggroScanInterval)
                {
                    TryAutoAttackNearbyEnemy();
                    lastAutoAggroScanTime = Time.time;
                }
            }

            if ((animator != null || unitSpriteRenderer != null) && Health.IsAlive)
            {
                UpdateAnimations();
            }
        }

        private bool CanConsiderAutoAggression()
        {
            if (currentCommand == null)
            {
                return true;
            }

            if (currentCommand is MoveCommand)
            {
                return (MoveComponent != null && MoveComponent.HasReachedDestination());
            }

            return false;
        }

        private void TryAutoAttackNearbyEnemy()
        {
            if (AttackComponent == null) return;

            LayerMask enemyMask;
            if (SelectionManager.Instances.TryGetValue(Team, out var sm))
            {
                enemyMask = sm.GetAttackableMask_DEBUG();
            }
            else
            {
                // Debug.LogWarning($"Юнит {gameObject.name} (команда {Team}) не смог получить SelectionManager для определения вражеской маски для авто-атаки.");
                // Пытаемся получить маску из первого попавшегося менеджера, если это вражеский юнит без своего SM.
                // Это не идеальное решение, но может сработать для простых сценариев.
                if (SelectionManager.Instances.Count > 0)
                {
                    enemyMask = SelectionManager.Instances.First().Value.GetAttackableMask_DEBUG();
                    // Тут бы хорошо проверить, что эта маска не включает своих же (если враг != команда 0)
                }
                else return; // Не можем определить врагов
            }

            float scanRadiusToUse = autoAggroScanRadius;

            var colliders = Physics2D.OverlapCircleAll(transform.position, scanRadiusToUse, enemyMask);
            IDamageable closestEnemy = null;
            float closestDistSqr = float.MaxValue;

            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;

                var damageable = col.GetComponentInSelfOrParent<IDamageable>();

                if (damageable != null && damageable.IsAlive &&
                    (damageable.TeamId != Team || damageable.TeamId == -1))
                {
                    if (AttackComponent.IsRanged)
                    {
                        if (!AttackComponent.HasClearLineOfSight(damageable))
                        {
                            continue;
                        }
                    }

                    float distSqr = (transform.position - damageable.MyTransform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        closestEnemy = damageable;
                    }
                }
            }

            if (closestEnemy != null)
            {
                // Debug.Log($"{gameObject.name} автоматически атакует {closestEnemy.MyGameObject.name}");
                SetCommand(new AttackCommand(this, closestEnemy));
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
            else if (currentCommand is MoveCommand)
            {
                currentCommand.Execute(this);
                if (MoveComponent != null && MoveComponent.HasReachedDestination())
                {
                    ClearCurrentCommand();
                }
            }
            else
            {
                currentCommand.Execute(this);
            }
        }

        private void UpdatePathDisplayForCurrentCommand()
        {
            if (pathDisplay == null || !pathDisplay.isActiveAndEnabled) return;

            if (!Selected || Health == null || !Health.IsAlive)
            {
                pathDisplay.SetVisible(false);
                return;
            }

            pathDisplay.SetVisible(true);

            if (currentCommand is AttackCommand attackCmd && attackCmd.IsTargetStillValid())
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.Attack);
                pathDisplay.SetAttackTargetOverride(attackCmd.GetTarget());
            }
            else if (currentCommand is AttackMoveCommand attackMoveCmd) // Изменено для получения позиции
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.AttackMove);
                pathDisplay.SetAttackTargetOverride(null);
                // Для AttackMoveCommand маркер будет на targetPosition команды
                // pathDisplay.SetDestinationMarkerPosition(attackMoveCmd.GetTargetPosition_DEBUG()); // Нужен такой метод в PathDisplay
            }
            else if (currentCommand is MoveCommand moveCmd) // Изменено для получения позиции
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.Default);
                pathDisplay.SetAttackTargetOverride(null);
                // Для MoveCommand маркер будет на targetPosition команды
                // pathDisplay.SetDestinationMarkerPosition(moveCmd.GetTargetPosition_DEBUG()); // Нужен такой метод в PathDisplay
            }
            else
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.None);
                pathDisplay.SetAttackTargetOverride(null);
            }
        }

        public void SetCommand(IUnitCommand cmd)
        {
            if (Health == null || !Health.IsAlive) return;

            if (currentCommand?.GetType() == cmd?.GetType())
            {
                if (currentCommand is MoveCommand oldMove && cmd is MoveCommand newMove &&
                    Vector3.Distance(oldMove.GetTargetPosition_DEBUG(), newMove.GetTargetPosition_DEBUG()) < 0.1f &&
                    (MoveComponent != null && MoveComponent.IsMoving()))
                {
                    return;
                }

                if (currentCommand is AttackCommand oldAttack && cmd is AttackCommand newAttack &&
                    oldAttack.GetTarget() == newAttack.GetTarget())
                {
                    return;
                }

                if (currentCommand is AttackMoveCommand oldAttackMove && cmd is AttackMoveCommand newAttackMove &&
                    Vector3.Distance(oldAttackMove.GetTargetPosition_DEBUG(), newAttackMove.GetTargetPosition_DEBUG()) <
                    0.1f)
                {
                    return;
                }
            }

            currentCommand = cmd;
            UpdatePathDisplayForCurrentCommand();
        }

        public void ClearCurrentCommand()
        {
            if (currentCommand is AttackCommand || currentCommand is AttackMoveCommand)
            {
                if (MoveComponent != null && MoveComponent.IsMoving())
                {
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
                Health.OnDeath -= HandleDeath;
            }

            if (AttackComponent != null)
            {
                AttackComponent.OnAttackPerformed -= HandleAttackPerformed;
            }

            // Убираем юнит из SelectionManager только если он еще там
            // (мог быть уже удален, если HandleDeath вызвался до OnDestroy)
            if (SelectionManager.Instances.TryGetValue(Team, out var manager))
            {
                // Проверка, существует ли еще объект в пуле менеджера, чтобы избежать ошибок при двойном удалении
                // Это может быть излишним, если RemoveUnitForTeam сам обрабатывает отсутствующие элементы.
                // Для простоты пока уберем проверку на наличие в pool перед удалением.
                SelectionManager.RemoveUnitForTeam(this, Team);
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
            EnableOutline();
            Selected = true;
            UpdatePathDisplayForCurrentCommand();
        }

        public void Deselect()
        {
            DisableOutline();
            Selected = false;
            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(false);
            }
        }

        public IUnitCommand GetCurrentCommand_DEBUG()
        {
            return currentCommand;
        }
    }
}