using AegisCore2D.GeneralScripts;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class Projectile : MonoBehaviour
    {
        private IDamageable target;
        private float damage;
        private float speed;
        private int ownerTeamId;
        private GameObject attacker; 

        private bool isInitialized = false; // Renamed for clarity
        private Vector3 lastKnownTargetPosition;
        private const float TimeToLive = 10f; // Max lifetime of projectile

        public void Initialize(IDamageable projectileTarget, float projectileDamage, float projectileSpeed,
                               int teamIdOfOwner, GameObject attackerGO)
        {
            this.target = projectileTarget;
            this.damage = projectileDamage;
            this.speed = projectileSpeed;
            this.ownerTeamId = teamIdOfOwner;
            this.attacker = attackerGO;

            if (target != null && target.IsAlive)
            {
                lastKnownTargetPosition = target.MyTransform.position;
                var direction = (target.MyTransform.position - transform.position).normalized;
                if (direction != Vector3.zero) // Prevent NaN rotation if target is at same position
                {
                    var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
            else 
            {
                // If target is invalid at launch, projectile might fly straight or self-destruct.
                // For now, assume it needs a valid target or initial direction.
                Debug.LogWarning($"Projectile initialized with invalid target from {attackerGO.name}. Destroying projectile.");
                DestroySelf(); 
                return;
            }

            isInitialized = true;
            Destroy(gameObject, TimeToLive); 
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (target != null && target.IsAlive)
            {
                lastKnownTargetPosition = target.MyTransform.position;
            }
            
            var direction = (lastKnownTargetPosition - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;
            
            // If projectile should home in (constantly turn towards target)
            // if (direction != Vector3.zero) {
            //     float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            //     transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            // }
            
            // Simple distance check for impact. Consider using a small trigger collider on projectile for better hit detection.
            var distanceToTargetPoint = Vector2.Distance(transform.position, lastKnownTargetPosition);
            if (distanceToTargetPoint < GetHitRadius()) // Use a hit radius
            {
                TryHitBasedOnProximity();
                DestroySelf();
            }
        }

        private void TryHitBasedOnProximity()
        {
            // This proximity check is less reliable than OnTriggerEnter2D.
            // It's a fallback if trigger detection isn't used or fails.
            if (target != null && target.IsAlive)
            {
                var actualDistanceToLiveTarget = Vector2.Distance(transform.position, target.MyTransform.position);
                if (actualDistanceToLiveTarget < GetHitRadius()) 
                {
                    if (target.TeamId != ownerTeamId || target.TeamId == -1) 
                    {
                        // Debug.Log($"Projectile (proximity) from {attacker?.name} hit {target.MyGameObject.name}"); // Optional
                        target.TakeDamage(damage, attacker);
                    }
                }
            }
        }

        private float GetHitRadius()
        {
            var col = GetComponent<CircleCollider2D>();
            if (col != null) return col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y); // Use max scale component
            return 0.3f; // Default hit radius
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isInitialized)
            {
                return;
            }

            if (attacker != null && other.gameObject == attacker)
            {
                return; // Pass through shooter
            }

            if (other.GetComponent<Projectile>() != null)
            {
                return; // Pass through other projectiles
            }
            
            var damageable = other.GetComponentInSelfOrParent<IDamageable>();

            if (damageable != null)
            {
                if (damageable.TeamId == ownerTeamId && ownerTeamId != -1) 
                {
                    // Debug.Log($"Projectile hit friendly: {damageable.MyGameObject.name}. Passing through."); // Optional
                    return; 
                }

                // Debug.Log($"Projectile (trigger) from {attacker?.name} hit {damageable.MyGameObject.name}"); // Optional
                damageable.TakeDamage(damage, attacker);
                DestroySelf(); 
            }
            else 
            {
                // Hit something not damageable, e.g., environment
                // Check layer if projectiles should be destroyed by obstacles
                // For example: if (other.gameObject.layer == LayerMask.NameToLayer("Obstacles"))
                // Debug.Log($"Projectile hit non-damageable object: {other.gameObject.name}. Destroying self."); // Optional
                DestroySelf(); // Destroy on any collision with non-damageable that isn't self/projectile
            }
        }

        private void DestroySelf()
        {
            // Potential: Instantiate hit effect particle/sound
            // e.g. if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            if (gameObject != null) // Check if not already destroyed
            {
                Destroy(gameObject);
            }
        }
    }
}