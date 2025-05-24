// AttackComponent.cs
using System;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackComponent : MonoBehaviour
    {
        [Header("General Stats")]
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float attackRange = 5.0f; // Теперь это общая дальность, для рендж будет основной
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Attack Type")]
        [SerializeField] private bool isRanged = false;

        [Header("Melee Specific (if not ranged)")]
        // Можно оставить attackRange выше как общую, или сделать отдельную meleeAttackRange, если isRanged = false.
        // Для простоты пока используем общую attackRange. Если isRanged = false, то attackRange - это melee range.

        [Header("Ranged Specific (if isRanged)")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint; // Точка, откуда вылетает снаряд
        [SerializeField] private float projectileSpeed = 10f; // Скорость снаряда

        private float currentCooldown = 0f;
        private HealthComponent healthComponent; 

        public event Action<IDamageable> OnAttackPerformed;
        public event Action OnRangedAttackLaunched; // Для анимаций/звуков выстрела

        void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("AttackComponent требует HealthComponent!", this);
                enabled = false;
                return;
            }

            if (isRanged)
            {
                if (projectilePrefab == null)
                {
                    Debug.LogError($"Ranged AttackComponent на {gameObject.name} не имеет projectilePrefab!", this);
                    isRanged = false; // Переключаем на мили, чтобы избежать ошибок
                }
                if (projectileSpawnPoint == null)
                {
                    Debug.LogWarning($"Ranged AttackComponent на {gameObject.name} не имеет projectileSpawnPoint! Будет использовать transform.position.", this);
                    // projectileSpawnPoint = transform; // Можно использовать transform юнита как запасной вариант
                }
            }
        }

        void Update()
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
                return false; // Не атакуем своих
            }

            float distanceToTarget = Vector2.Distance(transform.position, target.MyTransform.position);

            if (distanceToTarget <= attackRange)
            {
                if (isRanged)
                {
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

        private void PerformMeleeAttack(IDamageable target)
        {
            Debug.Log($"{gameObject.name} атакует (melee) {target.MyGameObject.name} на {damageAmount} урона.");
            target.TakeDamage(damageAmount, gameObject);
            
            currentCooldown = attackCooldown;
            OnAttackPerformed?.Invoke(target);
        }

        private void PerformRangedAttack(IDamageable target)
        {
            Transform spawnPoint = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
            GameObject projectileGO = Instantiate(projectilePrefab, spawnPoint.position, spawnPoint.rotation);
            Projectile projectileScript = projectileGO.GetComponent<Projectile>();

            if (projectileScript != null)
            {
                projectileScript.Initialize(target, damageAmount, projectileSpeed, healthComponent.TeamId, gameObject);
                Debug.Log($"{gameObject.name} выпускает снаряд в {target.MyGameObject.name}.");
                
                currentCooldown = attackCooldown;
                OnRangedAttackLaunched?.Invoke(); // Для звука/анимации выстрела
                OnAttackPerformed?.Invoke(target); // Общее событие атаки
            }
            else
            {
                Debug.LogError($"Префаб снаряда {projectilePrefab.name} не содержит компонент Projectile!", this);
                Destroy(projectileGO); // Уничтожаем неправильный снаряд
            }
        }

        public float GetAttackRange()
        {
            return attackRange;
        }

        public void Initialize(float newDamage, float newRange, float newCooldown, bool newIsRanged, GameObject projPrefab = null, float projSpeed = 0)
        {
            damageAmount = newDamage;
            attackRange = newRange;
            attackCooldown = newCooldown;
            isRanged = newIsRanged;
            if (isRanged) {
                projectilePrefab = projPrefab;
                projectileSpeed = projSpeed;
            }
        }
    }
}