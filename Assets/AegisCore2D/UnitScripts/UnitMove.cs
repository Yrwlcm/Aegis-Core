using Pathfinding;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(Seeker), typeof(AIPath))]
    public sealed class UnitMove : MonoBehaviour
    {
        public AIPath agent;

        private void Awake()
        {
            agent = GetComponent<AIPath>();
            agent.canMove = false;
        }
        
        public void StopAndHoldPosition() // Новое имя для ясности
        {
            agent.canMove = false;
            agent.canSearch = false; // Прекращаем поиск новых путей
            agent.destination = transform.position; // Явно указываем, что текущая позиция - цель
        }
        
        public void AllowMovementAndSearch()
        {
            agent.canMove = true;
            agent.canSearch = true;
        }

        public void MoveTo(Vector3 target)
        {
            AllowMovementAndSearch(); // Включаем перед установкой новой цели
            agent.destination = target;
            
            // Если уже движемся к этой же цели (с небольшой погрешностью), не перезапускаем путь
            if (agent.canMove && agent.hasPath && Vector3.Distance(agent.destination, target) < 0.1f)
            {
                return;
            }

            agent.destination = target;
            agent.canMove = true;
            // agent.SearchPath(); // AIPath обычно сам вызывает SearchPath при изменении destination, если enabled.
                                // Но для надежности можно вызвать, если он был выключен.
            if (!agent.pathPending && (!agent.hasPath)) 
            { // Если нет пути или он не валиден
                 agent.SearchPath();
            }
        }

        public void Stop()
        {
            agent.canMove = false;
            // agent.SetPath(null); // Очищает текущий путь, чтобы юнит резко остановился
            // или можно просто agent.destination = transform.position; и canMove = false
            agent.destination = transform.position; // Ставим текущую позицию как цель, чтобы он не пытался "добежать"
            // Если используется velocity для анимаций, его тоже нужно сбросить
            agent.FinalizeMovement(transform.position, Quaternion.identity); // Сбрасывает внутренние состояния скорости
        }

        public bool IsMoving()
        {
            // Проверяем, есть ли у агента путь, разрешено ли движение, и не достиг ли он конца пути
            // (agent.reachedEndOfPath может быть не всегда точным, если endReachedDistance маленький)
            // Лучше ориентироваться на desiredVelocity или remainingDistance
            return agent.canMove && agent.hasPath && agent.remainingDistance > agent.endReachedDistance;
            // или return agent.canMove && agent.desiredVelocity.sqrMagnitude > 0.01f;
        }

        public bool HasReachedDestination()
        {
            // Проверка, достиг ли юнит цели (может быть полезно для команд)
            return !agent.pathPending && agent.reachedEndOfPath;
        }
        
        public Vector3 GetDestination()
        {
            return agent.destination;
        }
    }
}