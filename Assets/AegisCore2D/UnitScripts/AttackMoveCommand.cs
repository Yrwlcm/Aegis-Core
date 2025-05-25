using AegisCore2D.GeneralScripts;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackMoveCommand : IUnitCommand
    {
        private readonly Vector3 targetPosition;

        public AttackMoveCommand(Vector3 target)
        {
            this.targetPosition = target;
        }

        public void Execute(Unit unit)
        {
            if (unit == null || unit.Health == null || !unit.Health.IsAlive)
            {
                unit?.ClearCurrentCommand();
                return;
            }

            if (unit.AttackComponent == null)
            {
                Debug.LogWarning($"{unit.name} (AttackMove): No AttackComponent. Clearing command.");
                unit.ClearCurrentCommand();
                return;
            }
            
            var actualScanRadius = unit.AttackComponent.GetAttackRange() * unit.AttackMoveScanRadiusMultiplier;
            actualScanRadius = Mathf.Max(actualScanRadius, unit.MinAttackMoveScanRadius);
            
            LayerMask enemyMask;
            if (SelectionManager.Instances.TryGetValue(unit.Team, out var sm))
            {
                enemyMask = sm.GetAttackableMask_DEBUG(); 
            }
            else 
            {
                Debug.LogWarning($"SelectionManager for team {unit.Team} not found for AttackMoveCommand. Using default layer for enemy scan.");
                enemyMask = LayerMask.GetMask("Default"); // Consider a more robust default or error handling
            }

            var closestEnemy = FindClosestEnemy(unit, actualScanRadius, enemyMask);

            if (closestEnemy != null && closestEnemy.IsAlive)
            {
                unit.SetCommand(new AttackCommand(unit, closestEnemy));
                return;
            }

            var moveComp = unit.MoveComponent;
            if (moveComp != null)
            {
                var distanceToTarget = Vector3.Distance(unit.transform.position, targetPosition);
                var endReachedDistance = moveComp.agent != null ? moveComp.agent.endReachedDistance : 0.1f;

                if (distanceToTarget > endReachedDistance)
                {
                    moveComp.MoveTo(targetPosition);
                }
                else
                {
                    if (moveComp.IsMoving())
                    {
                        moveComp.Stop(); 
                    }
                    unit.ClearCurrentCommand(); 
                }
            }
            else
            {
                Debug.LogWarning($"{unit.name} (AttackMove): No MoveComponent. Clearing command.");
                unit.ClearCurrentCommand();
            }
        }

        private IDamageable FindClosestEnemy(Unit executingUnit, float scanRadius, LayerMask enemyLayerMask)
        {
            var hitColliders = Physics2D.OverlapCircleAll(executingUnit.transform.position, scanRadius, enemyLayerMask);
            IDamageable closest = null;
            var minDistanceSqr = float.MaxValue;

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == executingUnit.gameObject) continue;

                var damageable = hitCollider.GetComponentInSelfOrParent<IDamageable>();

                if (damageable != null && damageable.IsAlive &&
                    (damageable.TeamId != executingUnit.Team || damageable.TeamId == -1)) 
                {
                    var distanceSqr = (executingUnit.transform.position - damageable.MyTransform.position).sqrMagnitude;
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