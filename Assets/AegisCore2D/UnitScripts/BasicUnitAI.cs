using UnityEngine;
using AegisCore2D.UnitScripts;
using AegisCore2D.GeneralScripts;

namespace AegisCore2D.AI
{
    [RequireComponent(typeof(Unit))]
    public class BasicUnitAI : MonoBehaviour
    {
        private enum AIState
        {
            Idle,                   // Состояние по умолчанию или ожидания
            MovingToInitialGoal,    // Движение к основной цели
            PatrollingAtGoal,       // Патрулирование в точке назначения
            EngagingTarget,         // Атака цели
            SearchingForNewTarget   // Поиск новой цели после потери текущей
        }

        [Header("AI Configuration")]
        [Tooltip("Основная точка, к которой будет стремиться ИИ.")]
        [SerializeField] private Vector3 initialGoalPosition;
        [Tooltip("Радиус вокруг initialGoalPosition для патрулирования.")]
        [SerializeField] private float patrolRadius = 5f;
        [Tooltip("Минимальное время ожидания перед следующим патрульным движением.")]
        [SerializeField] private float minPatrolWaitTime = 3f;
        [Tooltip("Максимальное время ожидания перед следующим патрульным движением.")]
        [SerializeField] private float maxPatrolWaitTime = 7f;
        [Tooltip("Как часто (в секундах) ИИ сканирует в поисках врагов.")]
        [SerializeField] private float enemyScanInterval = 0.5f;
        [Tooltip("Радиус \"поводка\": как далеко ИИ может отойти от initialGoalPosition, преследуя врага, прежде чем вернется.")]
        [SerializeField] private float engagementLeashRadius = 25f;
        [Tooltip("Множитель для радиуса обнаружения врагов относительно дальности атаки юнита.")]
        [SerializeField] private float aggroRadiusMultiplier = 1.5f;


        private Unit controlledUnit;
        private AttackComponent attackComponent;
        private UnitMove moveComponent;

        private AIState currentState = AIState.Idle;
        private float lastEnemyScanTime;
        private float nextPatrolActionTime;
        private IDamageable currentTarget;
        private Vector3 currentPatrolDestination;
        private bool hasReachedInitialGoal = false; // Достиг ли юнит основной цели хотя бы раз

        private void Awake()
        {
            controlledUnit = GetComponent<Unit>();
            if (controlledUnit == null)
            {
                Debug.LogError("BasicUnitAI требует компонент Unit на том же GameObject.", this);
                enabled = false;
                return;
            }

            // Получаем компоненты из Unit, а не через GetComponent снова, т.к. Unit их уже кэширует
            attackComponent = controlledUnit.AttackComponent;
            moveComponent = controlledUnit.MoveComponent;

            if (attackComponent == null || moveComponent == null)
            {
                Debug.LogError("Unit, управляемый BasicUnitAI, не имеет AttackComponent или MoveComponent.", this);
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            // Если initialGoalPosition не установлена (равна Vector3.zero),
            // ИИ будет патрулировать вокруг своей стартовой позиции.
            if (initialGoalPosition == Vector3.zero)
            {
                initialGoalPosition = transform.position;
            }
            
            // Определяем начальное состояние
            // Если мы уже у цели или очень близко
            if (Vector3.Distance(transform.position, initialGoalPosition) < moveComponent.agent.endReachedDistance * 1.5f) // Небольшой допуск
            {
                hasReachedInitialGoal = true;
                ChangeState(AIState.PatrollingAtGoal);
            }
            else
            {
                ChangeState(AIState.MovingToInitialGoal);
            }
        }

        private void Update()
        {
            if (controlledUnit == null || !controlledUnit.Health.IsAlive)
            {
                // Если юнит мертв, ИИ не должен работать.
                // Команды должны были очиститься при смерти юнита.
                if(enabled) enabled = false; // Отключаем компонент ИИ
                return;
            }

            // Периодическое сканирование врагов, если не заняты активной атакой
            if (currentState != AIState.EngagingTarget && Time.time > lastEnemyScanTime + enemyScanInterval)
            {
                var potentialTarget = ScanForEnemies();
                if (potentialTarget != null)
                {
                    currentTarget = potentialTarget;
                    ChangeState(AIState.EngagingTarget);
                    // Логика для EngagingTarget будет выполнена в следующем цикле Update или ниже в switch
                }
                lastEnemyScanTime = Time.time;
            }

            // Основная логика состояний
            switch (currentState)
            {
                case AIState.Idle:
                    HandleIdleState();
                    break;
                case AIState.MovingToInitialGoal:
                    HandleMovingToInitialGoalState();
                    break;
                case AIState.PatrollingAtGoal:
                    HandlePatrollingAtGoalState();
                    break;
                case AIState.EngagingTarget:
                    HandleEngagingTargetState();
                    break;
                case AIState.SearchingForNewTarget:
                    HandleSearchingForNewTargetState();
                    break;
            }
        }
        
        private void ChangeState(AIState newState)
        {
            if (currentState == newState && Application.isPlaying) return; // Не меняем на то же состояние в рантайме

            // Debug.Log($"{gameObject.name} AI: Смена состояния с {currentState} на {newState}", this);
            currentState = newState;

            // Логика при входе в новое состояние
            switch (newState)
            {
                case AIState.MovingToInitialGoal:
                    controlledUnit.SetCommand(new MoveCommand(initialGoalPosition));
                    // hasReachedInitialGoal здесь не меняем, т.к. мы только начали движение к ней
                    break;
                case AIState.PatrollingAtGoal:
                    if(moveComponent.IsMoving()) moveComponent.StopAndHoldPosition(); // Останавливаемся по прибытии
                    controlledUnit.ClearCurrentCommand(); 
                    ScheduleNextPatrolAction(true); // Запланировать первое патрульное действие немедленно или с задержкой
                    hasReachedInitialGoal = true; // Отмечаем, что достигли основной цели
                    break;
                case AIState.EngagingTarget:
                    if (currentTarget != null && currentTarget.IsAlive)
                    {
                        controlledUnit.SetCommand(new AttackCommand(controlledUnit, currentTarget));
                    }
                    else // Цель стала невалидной до того, как отдали команду
                    {
                        ChangeState(AIState.SearchingForNewTarget); // Ищем новую
                    }
                    break;
                case AIState.SearchingForNewTarget:
                    controlledUnit.ClearCurrentCommand(); // Прекращаем атаковать предыдущую (если была)
                    // Логика поиска новой цели будет в HandleSearchingForNewTargetState
                    break;
                case AIState.Idle:
                    controlledUnit.ClearCurrentCommand();
                    if(moveComponent.IsMoving()) moveComponent.StopAndHoldPosition();
                    break;
            }
        }

        private void HandleIdleState()
        {
            // По умолчанию, если есть initialGoalPosition, и мы от неё далеко, начинаем движение.
            // Иначе, переходим в патрулирование.
            if (hasReachedInitialGoal || Vector3.Distance(transform.position, initialGoalPosition) < patrolRadius * 0.5f)
            {
                ChangeState(AIState.PatrollingAtGoal);
            }
            else if (initialGoalPosition != transform.position) // Проверка, что цель не текущая позиция
            {
                 ChangeState(AIState.MovingToInitialGoal);
            }
            // Если initialGoalPosition - это текущая позиция, и hasReachedInitialGoal не true, то это странно, но останемся Idle.
        }

        private void HandleMovingToInitialGoalState()
        {
            // Сканирование врагов происходит глобально в Update. Если враг найден, состояние изменится на EngagingTarget.
            // Проверяем, достигли ли цели
            if (moveComponent.HasReachedDestination())
            {
                ChangeState(AIState.PatrollingAtGoal);
            }
            // Если текущая команда не движение к initialGoalPosition (например, была прервана), переустанавливаем.
            // Это нужно, если ИИ был отвлечен, но враг исчез до того, как состояние сменилось на EngagingTarget.
            else if (!(controlledUnit.GetCurrentCommand_DEBUG() is MoveCommand moveCmd && 
                       Vector3.Distance(moveCmd.GetTargetPosition_DEBUG(), initialGoalPosition) < 0.1f) )
            {
                 // Убедимся, что мы все еще в этом состоянии, прежде чем переназначать команду
                 if (currentState == AIState.MovingToInitialGoal) 
                 {
                    controlledUnit.SetCommand(new MoveCommand(initialGoalPosition));
                 }
            }
        }

        private void ScheduleNextPatrolAction(bool immediateFirstMove = false)
        {
            nextPatrolActionTime = Time.time + (immediateFirstMove ? 0 : Random.Range(minPatrolWaitTime, maxPatrolWaitTime));
            
            // Генерируем случайную точку внутри круга патрулирования
            var randomDir = Random.insideUnitCircle * patrolRadius;
            currentPatrolDestination = initialGoalPosition + new Vector3(randomDir.x, randomDir.y, 0);
            // Убедимся, что точка на NavMesh (A* Pathfinding Project)
            // Если используете A*, то лучше запросить ближайшую валидную точку к currentPatrolDestination
            // var node = AstarPath.active.GetNearest(currentPatrolDestination, NNConstraint.Default).node;
            // if (node != null) currentPatrolDestination = (Vector3)node.position;
            // Пока оставим так, UnitMove должен сам справиться с недостижимой точкой.
        }

        private void HandlePatrollingAtGoalState()
        {
            // Сканирование врагов происходит глобально в Update.
            if (Time.time >= nextPatrolActionTime)
            {
                // Если предыдущая команда на патрулирование завершилась или ее не было
                if (controlledUnit.GetCurrentCommand_DEBUG() == null || moveComponent.HasReachedDestination())
                {
                    controlledUnit.SetCommand(new MoveCommand(currentPatrolDestination));
                    ScheduleNextPatrolAction(); // Запланировать следующую точку после начала движения к текущей
                }
            }
            // Если юнит просто стоит (команда выполнена, ждет nextPatrolActionTime)
            else if (controlledUnit.GetCurrentCommand_DEBUG() == null && !moveComponent.IsMoving())
            {
                // Ничего не делаем, ждем
            }
        }

        private void HandleEngagingTargetState()
        {
            if (currentTarget == null || !currentTarget.IsAlive)
            {
                ChangeState(AIState.SearchingForNewTarget);
                return;
            }

            // Проверка "поводка": если ИИ слишком далеко от своей основной точки
            if (Vector3.Distance(transform.position, initialGoalPosition) > engagementLeashRadius)
            {
                // Debug.Log($"{gameObject.name} AI: Превышен радиус поводка. Отступаю от {currentTarget.MyGameObject.name}.", this);
                currentTarget = null; // Бросаем цель
                ChangeState(AIState.SearchingForNewTarget); // Это приведет к возвращению к MovingToInitialGoal или Patrolling
                return;
            }

            // Убеждаемся, что юнит атакует текущую цель.
            // Unit.AttackCommand сам должен разбираться с движением к цели, если она не в радиусе атаки.
            var currentCmd = controlledUnit.GetCurrentCommand_DEBUG();
            if (currentCmd is AttackCommand attackCmd)
            {
                if (attackCmd.GetTarget() != currentTarget) // Если команда на другую цель
                {
                    controlledUnit.SetCommand(new AttackCommand(controlledUnit, currentTarget));
                }
                // Если команда уже на текущую цель, то Update юнита сам ее обрабатывает.
            }
            else // Если текущая команда - не атака (или нет команды)
            {
                controlledUnit.SetCommand(new AttackCommand(controlledUnit, currentTarget));
            }
        }
        
        private void HandleSearchingForNewTargetState()
        {
            // Это состояние вызывается, когда currentTarget становится невалидным.
            // Пытаемся найти нового врага немедленно.
            var newTarget = ScanForEnemies();
            if (newTarget != null)
            {
                currentTarget = newTarget;
                ChangeState(AIState.EngagingTarget);
            }
            else
            {
                // Врагов не найдено. Возвращаемся к предыдущему мирному поведению.
                currentTarget = null;
                if (hasReachedInitialGoal)
                {
                    ChangeState(AIState.PatrollingAtGoal);
                }
                else
                {
                    ChangeState(AIState.MovingToInitialGoal);
                }
            }
        }

        private IDamageable ScanForEnemies()
        {
            var scanRadius = attackComponent.GetAttackRange() * aggroRadiusMultiplier;
            
            var colliders = Physics2D.OverlapCircleAll(transform.position, scanRadius);
            IDamageable closestEnemy = null;
            var closestDistSqr = float.MaxValue;

            foreach (var col in colliders)
            {
                // Пытаемся получить IDamageable на объекте или его родителе
                var damageable = col.GetComponentInSelfOrParent<IDamageable>(); 

                if (damageable != null && damageable.IsAlive && 
                    damageable.TeamId != controlledUnit.Team && // Не своя команда
                    damageable.TeamId != -1) // И не нейтральная команда (если -1 = нейтралы, которых не атакуем просто так)
                                             // Если -1 это "атакуемые всеми", то это условие надо убрать или изменить
                {
                    var distSqr = (transform.position - damageable.MyTransform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        closestEnemy = damageable;
                    }
                }
            }
            return closestEnemy;
        }

        /// <summary>
        /// Позволяет установить начальную цель для ИИ извне (например, при спауне).
        /// </summary>
        public void SetInitialGoalPosition(Vector3 goalPosition)
        {
            initialGoalPosition = goalPosition;
            hasReachedInitialGoal = false; // Сбрасываем флаг, т.к. цель новая

            // Если ИИ был в состоянии ожидания или патрулирования, переоцениваем состояние
            if (currentState == AIState.Idle || currentState == AIState.PatrollingAtGoal) 
            {
                if (Vector3.Distance(transform.position, initialGoalPosition) < moveComponent.agent.endReachedDistance * 1.5f)
                {
                     hasReachedInitialGoal = true; // Сразу отмечаем, если новая цель рядом
                     ChangeState(AIState.PatrollingAtGoal);
                } else {
                     ChangeState(AIState.MovingToInitialGoal);
                }
            }
            // Если ИИ уже двигался к старой цели (MovingToInitialGoal),
            // то при следующем Update этого состояния команда обновится на новую initialGoalPosition.
            // Если атаковал (EngagingTarget), то после боя вернется к новой цели.
        }

        private void OnDrawGizmosSelected()
        {
            // Не устанавливаем initialGoalPosition в OnDrawGizmosSelected, это может привести к неожиданному поведению.
            // Лучше делать это в Start или через публичный метод.

            // Отображение основной цели
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(initialGoalPosition, 0.5f);
            if(Application.isPlaying) Gizmos.DrawLine(transform.position, initialGoalPosition);

            // Отображение радиуса патрулирования
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(initialGoalPosition, patrolRadius);

            // Отображение текущей патрульной цели (если патрулируем)
            if (Application.isPlaying && currentState == AIState.PatrollingAtGoal)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentPatrolDestination, 0.3f);
                if (controlledUnit != null && controlledUnit.GetCurrentCommand_DEBUG() is MoveCommand)
                {
                     Gizmos.DrawLine(transform.position, currentPatrolDestination);
                }
            }

            // Отображение радиуса сканирования врагов
            if (attackComponent != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, attackComponent.GetAttackRange() * aggroRadiusMultiplier);
            }

            // Отображение радиуса "поводка"
            Gizmos.color = new Color(1f, 0.5f, 0f); // Оранжевый
            Gizmos.DrawWireSphere(initialGoalPosition, engagementLeashRadius);
        }
    }
}