// Projectile.cs

using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class Projectile : MonoBehaviour
    {
        private IDamageable target;
        private float damage;
        private float speed;
        private int ownerTeamId;
        private GameObject attacker; // Кто запустил снаряд

        private bool initialized = false;
        private Vector3 lastKnownTargetPosition; // Если цель умрет/исчезнет, лететь в ее последнюю точку

        public void Initialize(IDamageable projectileTarget, float projectileDamage, float projectileSpeed,
            int teamIdOfOwner, GameObject attackerGO)
        {
            target = projectileTarget;
            damage = projectileDamage;
            speed = projectileSpeed;
            ownerTeamId = teamIdOfOwner;
            attacker = attackerGO;

            if (target != null && target.IsAlive)
            {
                lastKnownTargetPosition = target.MyTransform.position;
                // Поворачиваем снаряд в сторону цели при запуске (опционально, если спрайт направленный)
                Vector2 direction = (target.MyTransform.position - transform.position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            else // Если цель уже невалидна при запуске, лететь прямо или в указанную точку (если бы была)
            {
                // Для простоты, если цели нет, можно самоуничтожиться или лететь вперед
                // lastKnownTargetPosition = transform.position + transform.right * 10f; // Пример: лететь вперед
                DestroySelf();
                return;
            }

            initialized = true;
            // Можно добавить время жизни снаряду, чтобы он не летел вечно
            Destroy(gameObject, 10f); // Самоуничтожение через 10 секунд, если не попал
        }

        void Update()
        {
            if (!initialized) return;

            // Обновляем lastKnownTargetPosition, если цель все еще жива
            if (target != null && target.IsAlive)
            {
                lastKnownTargetPosition = target.MyTransform.position;
            }
            // Если цели уже нет (умерла/уничтожена) или она не IDamageable, то target будет null
            // В этом случае снаряд продолжит лететь к lastKnownTargetPosition

            Vector3 direction = (lastKnownTargetPosition - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;

            // Постоянно поворачивать снаряд к цели (если это самонаводящийся снаряд)
            // Если снаряд должен лететь прямо после выстрела, эту часть закомментировать/убрать
            // float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            // Проверка столкновения (простая, по дистанции)
            // Для более точного столкновения лучше использовать коллайдеры (триггеры)
            float distanceToTargetPoint = Vector2.Distance(transform.position, lastKnownTargetPosition);

            if (distanceToTargetPoint < 0.2f) // Порог попадания
            {
                TryHitTarget();
                DestroySelf();
            }
        }

        void TryHitTarget()
        {
            // Проверяем, действительно ли мы попали в живую цель, а не просто долетели до точки
            if (target != null && target.IsAlive)
            {
                // Дополнительная проверка дистанции до актуальной позиции цели, если она еще жива
                float actualDistanceToLiveTarget = Vector2.Distance(transform.position, target.MyTransform.position);
                if (actualDistanceToLiveTarget < GetHitRadius()) // GetHitRadius() - радиус "поражения" снаряда
                {
                    // Проверяем, что не атакуем своих (хотя это должно было быть проверено при запуске, но для снарядов тоже полезно)
                    if (target.TeamId != ownerTeamId || target.TeamId == -1) // -1 для нейтральных целей
                    {
                        Debug.Log($"Снаряд от {attacker.name} попал в {target.MyGameObject.name}");
                        target.TakeDamage(damage, attacker);
                    }
                }
            }
            // Если цель уже умерла, а мы долетели до ее последней точки, ничего не делаем (урон не наносим)
        }

        // Можно определить радиус попадания снаряда
        float GetHitRadius()
        {
            // Если у снаряда есть коллайдер, можно использовать его радиус.
            // Пока просто константа.
            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col != null) return col.radius * transform.localScale.x; // Учитываем масштаб
            return 0.3f;
        }
        

        // Для столкновений через триггеры (если у снаряда есть Rigidbody2D (kinematic) и Collider2D (IsTrigger = true))
        void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log($"Projectile {gameObject.name} OnTriggerEnter2D with: {other.gameObject.name} " +
                      $"on layer {LayerMask.LayerToName(other.gameObject.layer)} Tag: {other.tag}");

            if (!initialized)
            {
                Debug.Log("  Projectile not initialized, trigger ignored.");
                return;
            }

            // 1. Игнорировать столкновение с самим стрелком
            if (attacker != null && other.gameObject == attacker)
            {
                Debug.Log("  Hit self (attacker). Passing through.");
                return; // Снаряд проходит сквозь того, кто его выпустил
            }

            // 2. Игнорировать столкновение с другими снарядами (если нужно)
            //    Для этого у префаба снаряда должен быть тег "Projectile" или он должен быть на слое "Projectiles"
            //    и здесь проверяем if (other.CompareTag("Projectile")) return;
            //    Или if (other.GetComponent<Projectile>() != null) return; // Как у тебя было
            if (other.GetComponent<Projectile>() != null)
            {
                Debug.Log("  Hit another projectile. Passing through.");
                return;
            }
            
            // 3. Получаем IDamageable у объекта, с которым столкнулись
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null) damageable = other.GetComponentInParent<IDamageable>(); // Проверяем и родителя

            if (damageable != null) // Если это объект, который может получать урон
            {
                Debug.Log(
                    $"  Found IDamageable: {damageable.MyGameObject.name}, Team: {damageable.TeamId}, OwnerTeam (projectile): {ownerTeamId}");

                // 4. Проверяем команду: если команда цели совпадает с командой владельца снаряда,
                //    и это не "нейтральная" команда (например, -1, которую все могут атаковать),
                //    то снаряд проходит сквозь.
                if (damageable.TeamId == ownerTeamId &&
                    ownerTeamId != -1) // Предполагаем, что -1 это нейтральная/атакуемая всеми команда
                {
                    Debug.Log("  Hit friendly unit or structure. Passing through.");
                    return; // Снаряд проходит сквозь союзника
                }

                // 5. Если дошли сюда, значит, это враг или нейтрал, которому можно нанести урон
                Debug.Log(
                    $"  Attempting to deal {damage} damage to {damageable.MyGameObject.name} by {attacker?.name}");
                damageable.TakeDamage(damage, attacker);
                DestroySelf(); // Уничтожаем снаряд после нанесения урона врагу
            }
            else // Если столкнулись с чем-то, что не является IDamageable (например, стена)
            {
                Debug.Log("No IDamageable component found on hit object or its parents.");
                // Если снаряд должен уничтожаться при столкновении с окружением:
                // Убедись, что у объектов окружения (стен и т.д.) есть коллайдеры на определенном слое.
                if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle")) // Замени на свои слои
                {
                    Debug.Log("  Hit environment/obstacle. Destroying self.");
                    DestroySelf();
                }
                // Если не окружение, то снаряд просто пролетает дальше (если нет других условий)
            }
        }


        void DestroySelf()
        {
            // Здесь можно добавить эффект взрыва/попадания перед уничтожением
            // Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}