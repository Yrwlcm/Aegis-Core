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

        private bool isInitialized = false;
        private Vector3 lastKnownTargetPosition;
        private const float TimeToLive = 10f;

        // Добавим поле для слоя препятствий, если нужно будет его настраивать извне,
        // но обычно он фиксированный.
        private LayerMask obstacleLayerMask; 

        public void Initialize(IDamageable projectileTarget, float projectileDamage, float projectileSpeed,
                               int teamIdOfOwner, GameObject attackerGO)
        {
            target = projectileTarget;
            damage = projectileDamage;
            speed = projectileSpeed;
            ownerTeamId = teamIdOfOwner;
            attacker = attackerGO;

            // Получаем маску слоя "Obstacle" один раз при инициализации
            obstacleLayerMask = LayerMask.GetMask("Obstacle"); // Убедись, что слой "Obstacle" существует

            if (target != null && target.IsAlive)
            {
                lastKnownTargetPosition = target.MyTransform.position;
                var direction = (target.MyTransform.position - transform.position).normalized;
                if (direction != Vector3.zero) 
                {
                    var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
            else 
            {
                Debug.LogWarning($"Projectile initialized with invalid target from {attackerGO?.name}. Destroying projectile.");
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
            
            var distanceToTargetPoint = Vector2.Distance(transform.position, lastKnownTargetPosition);
            if (distanceToTargetPoint < GetHitRadius())
            {
                TryHitBasedOnProximity(); // Этот метод менее надежен, чем триггеры
                DestroySelf();
            }
        }

        private void TryHitBasedOnProximity()
        {
            if (target != null && target.IsAlive)
            {
                var actualDistanceToLiveTarget = Vector2.Distance(transform.position, target.MyTransform.position);
                if (actualDistanceToLiveTarget < GetHitRadius()) 
                {
                    if (target.TeamId != ownerTeamId || target.TeamId == -1) 
                    {
                        target.TakeDamage(damage, attacker);
                    }
                }
            }
        }

        private float GetHitRadius()
        {
            var col = GetComponent<CircleCollider2D>();
            if (col != null) return col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            return 0.3f;
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isInitialized) return;
            if (attacker != null && other.gameObject == attacker) return;
            if (other.GetComponent<Projectile>() != null) return;
            
            // Проверка на столкновение с препятствием
            // (1 << other.gameObject.layer) проверяет, установлен ли бит слоя other.gameObject в obstacleLayerMask
            if (obstacleLayerMask != 0 && (obstacleLayerMask.value & (1 << other.gameObject.layer)) > 0)
            {
                // Debug.Log($"Projectile hit obstacle: {other.gameObject.name}. Destroying self."); // Опционально
                DestroySelf();
                return; // Важно выйти, чтобы не обрабатывать дальше как IDamageable
            }
            
            var damageable = other.GetComponentInSelfOrParent<IDamageable>();

            if (damageable != null)
            {
                if (damageable.TeamId == ownerTeamId && ownerTeamId != -1) 
                {
                    return; 
                }
                damageable.TakeDamage(damage, attacker);
                DestroySelf(); 
            }
            else 
            {
                // Если это не IDamageable и не Obstacle (уже проверено выше),
                // то это какой-то другой объект. По умолчанию снаряд тоже уничтожится.
                // Если нужно, чтобы он пролетал сквозь какие-то "декоративные" слои,
                // здесь можно добавить доп. проверки.
                // Debug.Log($"Projectile hit non-damageable/non-obstacle object: {other.gameObject.name}. Destroying self."); // Опционально
                DestroySelf();
            }
        }

        private void DestroySelf()
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }
}