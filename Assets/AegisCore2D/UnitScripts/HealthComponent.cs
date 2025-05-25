using System;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        // TeamID is now primarily managed by the Unit class if present,
        // but HealthComponent needs its own copy for IDamageable interface and independent operation.
        [SerializeField]private int _teamId; 

        public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth
        public event Action<GameObject> OnDeath;           // Attacker's GameObject

        public GameObject MyGameObject => gameObject;
        public Transform MyTransform => transform;
        public int TeamId => _teamId; // Use internal field
        public bool IsAlive => currentHealth > 0;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
            // Try to get team from Unit component if available, otherwise it needs to be set via Initialize or SetTeamId
            var unit = GetComponent<Unit>();
            if (unit != null)
            {
                _teamId = unit.Team; // Assuming Unit.Team getter is safe in Awake
            }
        }

        private void Start()
        {
            // Initial notification for UI elements like health bars
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
        
        public void SetTeamId(int newTeamId)
        {
            _teamId = newTeamId;
        }

        public void TakeDamage(float amount, GameObject attacker)
        {
            if (!IsAlive || amount <= 0) return;

            currentHealth -= amount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            // Debug.Log($"{gameObject.name} took {amount} damage. HP: {currentHealth}/{maxHealth}"); // Optional

            if (currentHealth <= 0)
            {
                Die(attacker);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0) return; 

            currentHealth += amount;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            // Debug.Log($"{gameObject.name} healed {amount}. HP: {currentHealth}/{maxHealth}"); // Optional
        }

        private void Die(GameObject attacker)
        {
            // Debug.Log($"{gameObject.name} has died."); // Опционально
            OnDeath?.Invoke(attacker); // Оповещаем подписчиков (включая Unit)

            // gameObject.SetActive(false); // Простой вариант
            // Destroy(gameObject, 0.1f); // <--- ЭТУ СТРОКУ НУЖНО УДАЛИТЬ ИЛИ ЗАКОММЕНТИРОВАТЬ
            //      Теперь Unit.cs будет сам решать, когда уничтожать объект
            //      после проигрывания анимации смерти.
        }

        public void Initialize(float newMaxHealth, int newTeamId)
        {
            maxHealth = newMaxHealth;
            currentHealth = maxHealth; // Reset health on initialize
            SetTeamId(newTeamId);
            OnHealthChanged?.Invoke(currentHealth, maxHealth); // Notify UI
        }
    }
}