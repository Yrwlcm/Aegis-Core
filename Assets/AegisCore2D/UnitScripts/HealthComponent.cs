using UnityEngine;
using System;
using AegisCore2D.UnitScripts;

namespace AegisCore2D
{
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        [Header("Team")]
        [SerializeField] private int teamId; // Будет браться из Unit или устанавливаться отдельно

        public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth
        public event Action<GameObject> OnDeath;           // GameObject атакующего, если есть

        public GameObject MyGameObject => gameObject;
        public Transform MyTransform => transform;
        public int TeamId => teamId;
        public bool IsAlive => currentHealth > 0;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;


        void Awake()
        {
            currentHealth = maxHealth;
        }

        void Start()
        {
            // Если этот компонент на юните, попытаемся взять teamId оттуда
            Unit unit = GetComponent<Unit>();
            if (unit != null)
            {
                teamId = unit.Team;
            }
            // Важно: оповестить UI о начальном состоянии здоровья
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
        
        public void SetTeamId(int newTeamId)
        {
            teamId = newTeamId;
        }

        public void TakeDamage(float amount, GameObject attacker)
        {
            if (!IsAlive) return; // Уже мертв

            currentHealth -= amount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Debug.Log($"{gameObject.name} took {amount} damage. Current HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                Die(attacker);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return; // Нельзя лечить мертвых

            currentHealth += amount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            Debug.Log($"{gameObject.name} healed for {amount}. Current HP: {currentHealth}/{maxHealth}");
        }

        private void Die(GameObject attacker)
        {
            Debug.Log($"{gameObject.name} has died.");
            OnDeath?.Invoke(attacker);

            // Базовая логика смерти - пока просто деактивируем
            // Позже здесь будет проигрывание анимации смерти, выпадение лута и т.д.
            // SelectionManager.RemoveUnitForTeam сам юнит должен позаботиться о вызове этого, либо здесь, если HealthComponent всегда на юните
            
            Unit unit = GetComponent<Unit>();
            if (unit != null)
            {
                // Юнит сам должен отписаться от SelectionManager при уничтожении/смерти.
                // Но если мы уничтожаем объект прямо здесь, то нужно это сделать.
                // Лучше, чтобы Unit слушал OnDeath и сам вызывал свои методы очистки.
            }

            // gameObject.SetActive(false); // Простой вариант
            Destroy(gameObject, 0.1f); // Или уничтожить с небольшой задержкой, чтобы другие системы успели отреагировать
        }

        // Метод для установки начальных значений, если нужно (например, из ScriptableObject)
        public void Initialize(float newMaxHealth, int newTeamId)
        {
            maxHealth = newMaxHealth;
            currentHealth = maxHealth;
            teamId = newTeamId;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}