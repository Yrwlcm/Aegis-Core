using System;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackComponent : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float attackRange = 1.0f; // Дальность атаки для милишников
        [SerializeField] private float attackCooldown = 1.5f; // Время между атаками

        // Позже добавим для ranged:
        // [SerializeField] private GameObject projectilePrefab;
        // [SerializeField] private Transform projectileSpawnPoint;
        // [SerializeField] private bool isRanged = false;

        private float currentCooldown = 0f;
        private HealthComponent healthComponent; // Для получения TeamId

        public event Action<IDamageable> OnAttackPerformed; // Событие, когда атака фактически совершена

        void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("AttackComponent требует HealthComponent на том же GameObject для определения команды!", this);
                enabled = false;
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

        /// <summary>
        /// Пытается атаковать цель.
        /// </summary>
        /// <param name="target">Цель для атаки.</param>
        /// <returns>True, если атака была инициирована (цель в радиусе и кулдаун прошел), иначе false.</returns>
        public bool TryAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive)
            {
                //Debug.LogWarning($"{gameObject.name} пытается атаковать null или мертвую цель.", this);
                return false;
            }

            if (healthComponent.TeamId == target.TeamId && target.TeamId != -1) // -1 может быть нейтральной командой
            {
                //Debug.LogWarning($"{gameObject.name} пытается атаковать союзника ({target.MyGameObject.name}).", this);
                return false; // Не атакуем своих
            }

            if (!CanAttack())
            {
                //Debug.Log($"{gameObject.name} на кулдауне, не может атаковать.", this);
                return false;
            }

            float distanceToTarget = Vector2.Distance(transform.position, target.MyTransform.position);

            if (distanceToTarget <= attackRange)
            {
                // Пока только милишная атака
                PerformMeleeAttack(target);
                return true;
            }
            else
            {
                //Debug.Log($"{gameObject.name}: цель ({target.MyGameObject.name}) слишком далеко ({distanceToTarget} > {attackRange}).", this);
                return false; // Цель слишком далеко
            }
        }

        private void PerformMeleeAttack(IDamageable target)
        {
            Debug.Log($"{gameObject.name} атакует (melee) {target.MyGameObject.name} на {damageAmount} урона.");
            target.TakeDamage(damageAmount, gameObject); // gameObject этого AttackComponent - атакующий
            
            currentCooldown = attackCooldown;
            OnAttackPerformed?.Invoke(target); // Оповещаем о совершенной атаке
            
            // Здесь позже можно будет запустить анимацию атаки
            // unitAnimationController.PlayAttackAnimation();
        }

        // Заглушка для GetAttackRange, чтобы другие компоненты (например, ИИ) могли его узнать
        public float GetAttackRange()
        {
            return attackRange;
        }

        // Метод для установки статов, если нужно (например, из ScriptableObject)
        public void Initialize(float newDamage, float newRange, float newCooldown)
        {
            damageAmount = newDamage;
            attackRange = newRange;
            attackCooldown = newCooldown;
        }
    }
}