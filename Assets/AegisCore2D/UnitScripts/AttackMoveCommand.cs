// --- START OF FILE AttackMoveCommand.cs ---

using AegisCore2D.GeneralScripts;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackMoveCommand : IUnitCommand
    {
        private readonly Vector3 targetPosition;
        // Радиус сканирования и маска врагов теперь передаются не напрямую,
        // а будут браться из юнита или его компонентов при выполнении.
        // private readonly float scanRadius; // Убрали
        // private readonly LayerMask enemyLayerMask; // Убрали

        public AttackMoveCommand(Vector3 target) // Конструктор упрощен
        {
            this.targetPosition = target;
        }

        public void Execute(Unit unit)
        {
            if (unit == null || !unit.Health.IsAlive)
            {
                unit?.ClearCurrentCommand();
                return;
            }

            // Получаем параметры сканирования из юнита
            float actualScanRadius = unit.AttackComponent.GetAttackRange() * unit.AttackMoveScanRadiusMultiplier;
            actualScanRadius = Mathf.Max(actualScanRadius, unit.MinAttackMoveScanRadius);
            
            // Маску врагов можно брать из SelectionManager или передавать как параметр, если она динамическая.
            // Для простоты, если она всегда одна, можно ее захардкодить или передать из SelectionManager при создании команды.
            // Но раз мы ее убрали из конструктора, нужно ее как-то получить.
            // Предположим, что у SelectionManager есть публичное свойство или метод для получения attackableMask.
            // Это не самый лучший дизайн, но для примера:
            LayerMask enemyMask;
            if (SelectionManager.Instances.TryGetValue(unit.Team, out var sm)) // Пытаемся получить менеджер команды юнита
            {
                enemyMask = sm.GetAttackableMask_DEBUG(); // Нужен такой метод в SelectionManager
            }
            else // Если менеджер не найден (маловероятно для игрока), используем дефолтную маску
            {
                Debug.LogWarning($"SelectionManager for team {unit.Team} not found for AttackMoveCommand. Using default layer.");
                enemyMask = LayerMask.GetMask("Default"); // Или твой слой врагов по умолчанию
            }


            IDamageable closestEnemy = FindClosestEnemy(unit, actualScanRadius, enemyMask);

            if (closestEnemy != null && closestEnemy.IsAlive)
            {
                unit.SetCommand(new AttackCommand(unit, closestEnemy));
                return;
            }

            UnitMove moveComp = unit.MoveComponent;
            if (moveComp != null)
            {
                float distanceToTarget = Vector3.Distance(unit.transform.position, targetPosition);
                // Используем agent.endReachedDistance для более точного определения достижения цели
                float endReachedDistance = moveComp.agent != null ? moveComp.agent.endReachedDistance : 0.1f;

                if (distanceToTarget > endReachedDistance)
                {
                    moveComp.MoveTo(targetPosition);
                }
                else
                {
                    if (moveComp.IsMoving())
                    {
                        moveComp.Stop(); // Останавливаемся, если достигли цели
                    }
                    unit.ClearCurrentCommand(); // Команда выполнена
                }
            }
            else
            {
                Debug.LogWarning($"{unit.name} (AttackMove): Нет MoveComponent. Команда отменена.");
                unit.ClearCurrentCommand();
            }
        }

        private IDamageable FindClosestEnemy(Unit executingUnit, float scanRadius, LayerMask enemyLayerMask)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(executingUnit.transform.position, scanRadius, enemyLayerMask);
            IDamageable closest = null;
            float minDistanceSqr = float.MaxValue;

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == executingUnit.gameObject) continue;

                IDamageable damageable = hitCollider.GetComponent<IDamageable>();
                if (damageable == null) damageable = hitCollider.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable.IsAlive &&
                    (damageable.TeamId != executingUnit.Team || damageable.TeamId == -1)) // -1 для нейтральных целей
                {
                    float distanceSqr = (executingUnit.transform.position - damageable.MyTransform.position).sqrMagnitude;
                    if (distanceSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distanceSqr;
                        closest = damageable;
                    }
                }
            }
            return closest;
        }

        public Vector3 GetTargetPosition_DEBUG()
        {
            return targetPosition;
        }
    }
}
// --- END OF FILE AttackMoveCommand.cs ---