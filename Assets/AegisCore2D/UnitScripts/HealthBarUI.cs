using UnityEngine;
using UnityEngine.UI;

namespace AegisCore2D.UnitScripts
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        private HealthComponent healthComponentToTrack;

        [Header("Sprite/Color Configuration")]
        [SerializeField] private Color fullHealthColor = Color.green;
        [SerializeField] private Color midHealthColor = Color.yellow;
        [SerializeField] private Color lowHealthColor = Color.red;

        [Tooltip("Threshold for mid health color (e.g., 0.6 for 60%)")]
        [SerializeField] private float midHealthThreshold = 0.6f;
        [Tooltip("Threshold for low health color (e.g., 0.3 for 30%)")]
        [SerializeField] private float lowHealthThreshold = 0.3f;
        
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (fillImage == null)
            {
                Debug.LogError("Fill Image not assigned in HealthBarUI!", this);
                enabled = false; 
            }
        }

        private void OnEnable()
        {
            SubscribeToHealthComponentEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromHealthComponentEvents();
        }
        
        private void LateUpdate()
        {
            if (mainCamera != null && healthComponentToTrack != null && healthComponentToTrack.IsAlive) // Ensure billboard faces camera
            {
                // Basic billboard effect for UI in world space
                transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                                 mainCamera.transform.rotation * Vector3.up);
            }
        }

        public void SetHealthComponent(HealthComponent healthComponent)
        {
            UnsubscribeFromHealthComponentEvents(); // Unsubscribe from old one first

            healthComponentToTrack = healthComponent;

            if (healthComponentToTrack != null)
            {
                SubscribeToHealthComponentEvents();
                UpdateHealthDisplay(healthComponentToTrack.CurrentHealth, healthComponentToTrack.MaxHealth);
                gameObject.SetActive(true); 
            }
            else
            {
                gameObject.SetActive(false); 
            }
        }

        private void SubscribeToHealthComponentEvents()
        {
            if (healthComponentToTrack != null)
            {
                healthComponentToTrack.OnHealthChanged += UpdateHealthDisplay;
                healthComponentToTrack.OnDeath += HandleTargetDeath;
                // Initial update in case health already set
                UpdateHealthDisplay(healthComponentToTrack.CurrentHealth, healthComponentToTrack.MaxHealth);
            }
        }

        private void UnsubscribeFromHealthComponentEvents()
        {
             if (healthComponentToTrack != null)
            {
                healthComponentToTrack.OnHealthChanged -= UpdateHealthDisplay;
                healthComponentToTrack.OnDeath -= HandleTargetDeath;
            }
        }

        private void UpdateHealthDisplay(float currentHealth, float maxHealth)
        {
            if (fillImage == null) return; 
            
            if (healthComponentToTrack == null) 
            {
                if (gameObject != null) Destroy(gameObject); // Orphaned bar
                return;
            }
            // HandleTargetDeath is responsible for destroying the bar when the unit dies.
            // This method just updates visuals if it's called.

            var fillAmount = (maxHealth > 0) ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            fillImage.fillAmount = fillAmount;

            if (fillAmount > midHealthThreshold) fillImage.color = fullHealthColor;
            else if (fillAmount > lowHealthThreshold) fillImage.color = midHealthColor;
            else fillImage.color = lowHealthColor;
        }
        
        private void HandleTargetDeath(GameObject attacker)
        {
            // Can add delay or animation before destroying
            if (gameObject != null) // Check if not already destroyed
            {
                 Destroy(gameObject);
            }
        }
    }
}