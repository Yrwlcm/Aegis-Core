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
                Debug.LogError("AttackComponent requires an HealthComponent on the same GameObject.", this);
                enabled = false;
                return;
            }

            if (isRanged)
            {
                if (projectilePrefab == null)
                {
                    Debug.LogError($"Ranged AttackComponent on {gameObject.name} is missing projectilePrefab!", this);
                    isRanged = false; // Fallback to melee to prevent runtime errors
                }
                if (projectileSpawnPoint == null)
                {
                    Debug.LogWarning($"Ranged AttackComponent on {gameObject.name} uses self as projectileSpawnPoint (not assigned).", this);
                    projectileSpawnPoint = transform; 
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

            // Prevent attacking own team (unless team ID is -1, indicating neutral/universally attackable)
            if (healthComponent.TeamId == target.TeamId && target.TeamId != -1) 
            {
                return false; 
            }

            var distanceToTarget = Vector2.Distance(transform.position, target.MyTransform.position);

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
            // Debug.Log($"{gameObject.name} melee attacks {target.MyGameObject.name} for {damageAmount} damage."); // Optional
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
                // Debug.Log($"{gameObject.name} launches projectile at {target.MyGameObject.name}."); // Optional
                
                currentCooldown = attackCooldown;
                OnRangedAttackLaunched?.Invoke();
                OnAttackPerformed?.Invoke(target);
            }
            else
            {
                Debug.LogError($"Projectile prefab {projectilePrefab.name} is missing the Projectile component!", this);
                Destroy(projectileGO);
            }
        }

        public float GetAttackRange()
        {
            return attackRange;
        }

        public void Initialize(float newDamage, float newRange, float newCooldown, bool newIsRanged, GameObject projPrefab = null, float projSpeed = 0f)
        {
            damageAmount = newDamage;
            attackRange = newRange;
            attackCooldown = newCooldown;
            isRanged = newIsRanged;
            if (isRanged) 
            {
                projectilePrefab = projPrefab;
                projectileSpeed = projSpeed;
            }
        }
    }
}