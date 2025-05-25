using System;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackComponent : MonoBehaviour
    {
        [Header("General Stats")]
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float attackRange = 5.0f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Attack Type")]
        [SerializeField] private bool isRanged = false;

        [Header("Line of Sight (for Ranged)")]
        [Tooltip("Слой, который блокирует линию огня для атак дальнего боя.")]
        [SerializeField] private LayerMask lineOfSightMask; // Сюда нужно будет добавить слой "Obstacle"

        [Header("Ranged Specific (if isRanged)")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float projectileSpeed = 10f;

        private float currentCooldown = 0f;
        private HealthComponent healthComponent;

        public event Action<IDamageable> OnAttackPerformed;
        public event Action OnRangedAttackLaunched;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("AttackComponent требует HealthComponent на том же GameObject.", this);
                enabled = false;
                return;
            }

            if (isRanged)
            {
                if (projectilePrefab == null)
                {
                    Debug.LogError($"Ranged AttackComponent на {gameObject.name} не имеет projectilePrefab!", this);
                    isRanged = false; 
                }
                if (projectileSpawnPoint == null)
                {
                    // Debug.LogWarning($"Ranged AttackComponent на {gameObject.name} использует себя как projectileSpawnPoint (не назначен).", this);
                    projectileSpawnPoint = transform;
                }
                if (lineOfSightMask == 0 && isRanged) // Если маска не настроена, а юнит дальнего боя
                {
                    Debug.LogWarning($"Ranged AttackComponent на {gameObject.name} не имеет настроенной lineOfSightMask. " +
                                     $"Рекомендуется настроить для корректной работы линии огня. " +
                                     $"По умолчанию будет использован слой 'Default' или 'Obstacle', если они есть.", this);
                    // Попытка установить маску по умолчанию, если не задана в инспектоre
                    int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                    if (obstacleLayer != -1) lineOfSightMask = 1 << obstacleLayer; // Устанавливаем только слой Obstacle
                    else
                    {
                        int defaultLayer = LayerMask.NameToLayer("Default");
                        if (defaultLayer != -1) lineOfSightMask = 1 << defaultLayer;
                    }

                }
            }
        }

        private void Update()
        {
            if (currentCooldown > 0)
            {
                currentCooldown -= Time.deltaTime;
            }
        }

        public bool CanAttack()
        {
            return currentCooldown <= 0;
        }

        public bool TryAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive || !CanAttack())
            {
                return false;
            }

            if (healthComponent.TeamId == target.TeamId && target.TeamId != -1)
            {
                return false; 
            }

            var distanceToTarget = Vector2.Distance(transform.position, target.MyTransform.position);

            if (distanceToTarget <= attackRange)
            {
                // Проверка линии огня для юнитов дальнего боя
                if (isRanged)
                {
                    if (!HasLineOfSight(target))
                    {
                        // Debug.Log($"{gameObject.name} не может атаковать {target.MyGameObject.name}: нет линии огня.");
                        return false; // Нет линии огня
                    }
                    PerformRangedAttack(target);
                }
                else
                {
                    PerformMeleeAttack(target);
                }
                return true;
            }
            return false;
        }

        private bool HasLineOfSight(IDamageable target)
        {
            if (!isRanged) return true; // Для мили-атак линия огня всегда есть (в пределах досягаемости)
            if (lineOfSightMask == 0) return true; // Если маска не задана, считаем, что линия огня есть

            var spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            var targetPos = target.MyTransform.position; // Можно целиться в центр коллайдера цели, если нужно точнее

            // Пытаемся получить коллайдер цели, чтобы не попасть в него при Linecast
            Collider2D targetCollider = target.MyGameObject.GetComponent<Collider2D>();
            if (targetCollider != null) targetCollider.enabled = false; // Временно отключаем коллайдер цели

            RaycastHit2D hit = Physics2D.Linecast(spawnPos, targetPos, lineOfSightMask);

            if (targetCollider != null) targetCollider.enabled = true; // Включаем коллайдер цели обратно

            if (hit.collider != null)
            {
                // Проверяем, не является ли препятствие частью самой цели (маловероятно с отключением коллайдера, но для полноты)
                // IDamageable hitDamageable = hit.collider.GetComponentInParent<IDamageable>();
                // if (hitDamageable == target) return true; // Попали в саму цель, игнорируем как препятствие
                
                // Debug.Log($"{gameObject.name} -> {target.MyGameObject.name}: Линия огня заблокирована {hit.collider.name}");
                return false; // Линия огня заблокирована
            }
            return true; // Линия огня свободна
        }


        private void PerformMeleeAttack(IDamageable target)
        {
            target.TakeDamage(damageAmount, gameObject);
            currentCooldown = attackCooldown;
            OnAttackPerformed?.Invoke(target);
        }

        private void PerformRangedAttack(IDamageable target)
        {
            var spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
            var projectileGO = Instantiate(projectilePrefab, spawnPoint.position, spawnPoint.rotation);
            var projectileScript = projectileGO.GetComponent<Projectile>();

            if (projectileScript != null)
            {
                projectileScript.Initialize(target, damageAmount, projectileSpeed, healthComponent.TeamId, gameObject);
                currentCooldown = attackCooldown;
                OnRangedAttackLaunched?.Invoke();
                OnAttackPerformed?.Invoke(target);
            }
            else
            {
                Debug.LogError($"Префаб снаряда {projectilePrefab.name} не содержит компонент Projectile!", this);
                Destroy(projectileGO);
            }
        }

        public float GetAttackRange()
        {
            return attackRange;
        }

        public void Initialize(float newDamage, float newRange, float newCooldown, bool newIsRanged, GameObject projPrefab = null, float projSpeed = 0f, LayerMask losMask = default)
        {
            damageAmount = newDamage;
            attackRange = newRange;
            attackCooldown = newCooldown;
            isRanged = newIsRanged;
            if (isRanged)
            {
                projectilePrefab = projPrefab;
                projectileSpeed = projSpeed;
                if (losMask != default) lineOfSightMask = losMask; // Если передана новая маска, используем ее
            }
        }
    }
}