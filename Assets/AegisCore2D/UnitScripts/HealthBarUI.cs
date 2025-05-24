// HealthBarUI.cs

using UnityEngine;
using UnityEngine.UI;

namespace AegisCore2D.UnitScripts
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private HealthComponent healthComponentToTrack; // Будет устанавливаться извне

        [Header("Sprite/Color Configuration")]
        
        [SerializeField] private Color fullHealthColor = Color.green;
        [SerializeField] private Color midHealthColor = Color.yellow; // Или оранжевый
        [SerializeField] private Color lowHealthColor = Color.red;

        [Tooltip("Порог для среднего здоровья (например, 0.6 для 60%)")]
        [SerializeField] private float midHealthThreshold = 0.6f;
        [Tooltip("Порог для низкого здоровья (например, 0.3 для 30%)")]
        [SerializeField] private float lowHealthThreshold = 0.3f;
        
        private Camera mainCamera; // Кэшируем камеру для Billboard эффекта

        void Awake()
        {
            mainCamera = Camera.main; // Или твоя RTS камера, если она не main
            if (fillImage == null)
            {
                Debug.LogError("Fill Image не назначен в HealthBarUI!", this);
                enabled = false; // Отключаем компонент, если нет fillImage
            }
        }

        void OnEnable()
        {
            if (healthComponentToTrack != null)
            {
                healthComponentToTrack.OnHealthChanged += UpdateHealthDisplay;
                healthComponentToTrack.OnDeath += HandleTargetDeath;
                UpdateHealthDisplay(healthComponentToTrack.CurrentHealth, healthComponentToTrack.MaxHealth); // Обновить при активации
            }
        }

        void OnDisable()
        {
            if (healthComponentToTrack != null)
            {
                healthComponentToTrack.OnHealthChanged -= UpdateHealthDisplay;
                healthComponentToTrack.OnDeath -= HandleTargetDeath;
            }
        }
        
        // Для Billboard эффекта, чтобы HP бар всегда был повернут к камере
        void LateUpdate()
        {
            if (mainCamera != null)
            {
                transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                                 mainCamera.transform.rotation * Vector3.up);
            }
        }

        public void SetHealthComponent(HealthComponent healthComponent)
        {
            // Отписываемся от старого, если был
            if (healthComponentToTrack != null)
            {
                healthComponentToTrack.OnHealthChanged -= UpdateHealthDisplay;
                healthComponentToTrack.OnDeath -= HandleTargetDeath;
            }

            healthComponentToTrack = healthComponent;

            if (healthComponentToTrack != null)
            {
                healthComponentToTrack.OnHealthChanged += UpdateHealthDisplay;
                healthComponentToTrack.OnDeath += HandleTargetDeath;
                // Немедленно обновить отображение
                UpdateHealthDisplay(healthComponentToTrack.CurrentHealth, healthComponentToTrack.MaxHealth);
                gameObject.SetActive(true); // Показать HP бар
            }
            else
            {
                gameObject.SetActive(false); // Скрыть, если нет цели
            }
        }

        private void UpdateHealthDisplay(float currentHealth, float maxHealth)
        {
            if (fillImage == null || healthComponentToTrack == null)
            {
                // Если healthComponentToTrack == null, возможно, юнит уже уничтожен,
                // и этот HealthBarUI скоро тоже будет уничтожен.
                if(healthComponentToTrack == null && gameObject != null)
                {
                    Destroy(gameObject); // Если цели нет, уничтожаем и бар
                }
                return;
            }

            if (!healthComponentToTrack.IsAlive)
            {
                // Если юнит мертв, HealthBarUI должен быть уничтожен через HandleTargetDeath.
                // Эта ветка больше не должна скрывать объект, так как он будет уничтожен.
                // gameObject.SetActive(false); // УБИРАЕМ ЭТО
                // Если HandleTargetDeath еще не вызван, а мы уже здесь,
                // то можно инициировать уничтожение. Но лучше полагаться на HandleTargetDeath.
                return; // Просто выходим, ожидая уничтожения
            }
            if(!gameObject.activeSelf && healthComponentToTrack.IsAlive) 
            {
                // Эта логика нужна, если бар мог быть деактивирован по другой причине
                // и теперь его нужно показать. Но при стратегии "уничтожать при смерти"
                // она менее актуальна.
                gameObject.SetActive(true);
            }

            if (fillImage == null || healthComponentToTrack == null) return;

            if (!healthComponentToTrack.IsAlive)
            {
                gameObject.SetActive(false); // Скрываем HP бар, если юнит мертв
                return;
            }
            else
            {
                 // Если HP бар был скрыт (например, после смерти и респауна), показываем его снова
                if(!gameObject.activeSelf) gameObject.SetActive(true);
            }


            float fillAmount = currentHealth / maxHealth;
            fillImage.fillAmount = fillAmount;

            if (fillAmount > midHealthThreshold)
            {
                fillImage.color = fullHealthColor;
            }
            else if (fillAmount > lowHealthThreshold)
            {
                fillImage.color = midHealthColor;
            }
            else
            {
                fillImage.color = lowHealthColor;
            }
        }
        
        private void HandleTargetDeath(GameObject attacker)
        {
            // Можно добавить задержку перед уничтожением, если нужна анимация смерти бара
            // Destroy(gameObject, 0.1f); // Например, с задержкой 0.1 секунды
            Destroy(gameObject); // Уничтожаем игровой объект HP бара
        }
    }
}