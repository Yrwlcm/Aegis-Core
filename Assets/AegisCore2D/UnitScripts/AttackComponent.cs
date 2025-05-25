using System;
using System.Collections.Generic;
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
        public bool IsRanged => isRanged;

        [Header("Line of Sight (for Ranged)")]
        [Tooltip("Слой, который блокирует линию огня для атак дальнего боя.")]
        [SerializeField] private LayerMask lineOfSightMask; 

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
                    projectileSpawnPoint = transform; 
                }
                if (lineOfSightMask.value == 0) // Проверяем .value, т.к. пустая маска это 0
                {
                    Debug.LogWarning($"Ranged AttackComponent на {gameObject.name} не имеет настроенной lineOfSightMask. " +
                                     $"Автоматическая атака и проверка LOS могут работать некорректно. " +
                                     $"Попытка установить слой 'Obstacle'.", this);
                    int obstacleLayer = LayerMask.NameToLayer("Obstacle");
                    if (obstacleLayer != -1) lineOfSightMask = 1 << obstacleLayer;
                    else Debug.LogError("Слой 'Obstacle' не найден. LineOfSightMask не установлена.", this);
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
                if (isRanged)
                {
                    if (!HasClearLineOfSight(target)) // Используем публичный метод
                    {
                        return false; 
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

        /// <summary>
        /// Проверяет, есть ли прямая видимость до цели для юнитов дальнего боя.
        /// </summary>
        public bool HasClearLineOfSight(IDamageable target)
        {
            if (!isRanged) return true; 
            if (lineOfSightMask.value == 0) return true; // Если маска не задана, считаем, что LOS есть (хотя это не идеально)
            if (target == null || target.MyGameObject == null) return false;


            var spawnPos = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            var targetPos = target.MyTransform.position; 

            // Для более точной проверки можно целиться в центр коллайдера цели, если он есть
            Collider2D targetMainCollider = target.MyGameObject.GetComponent<Collider2D>();
            if(targetMainCollider != null) targetPos = targetMainCollider.bounds.center;


            // Временно отключаем все коллайдеры на цели и ее дочерних объектах, чтобы Linecast не попал в них.
            // Это упрощенный подход. Более надежно - использовать слои и `ContactFilter2D` или `QueryTriggerInteraction.Ignore`.
            List<Collider2D> targetColliders = new List<Collider2D>();
            target.MyGameObject.GetComponentsInChildren<Collider2D>(true, targetColliders); // true - include inactive
            
            List<bool> originalStates = new List<bool>();
            foreach(var col in targetColliders)
            {
                originalStates.Add(col.enabled);
                col.enabled = false;
            }
            
            RaycastHit2D hit = Physics2D.Linecast(spawnPos, targetPos, lineOfSightMask);

            // Восстанавливаем состояние коллайдеров цели
            for(int i=0; i< targetColliders.Count; i++)
            {
                if(targetColliders[i] != null) // На случай если коллайдер был уничтожен пока был выключен (маловероятно)
                   targetColliders[i].enabled = originalStates[i];
            }
            
            if (hit.collider != null)
            {
                // Debug.Log($"{gameObject.name} -> {target.MyGameObject.name}: Линия огня заблокирована {hit.collider.name} на слое {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                return false; 
            }
            return true; 
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
                OnAttackPerformed?.Invoke(target); // OnAttackPerformed для обоих типов атак
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
                if (losMask.value != 0) lineOfSightMask = losMask; 
            }
        }
    }
}