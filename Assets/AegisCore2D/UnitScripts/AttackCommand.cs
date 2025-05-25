using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackCommand : IUnitCommand
    {
        private readonly IDamageable target;
        private readonly Unit executingUnit;

        public AttackCommand(Unit unit, IDamageable target)
        {
            executingUnit = unit;
            this.target = target;
        }

        public void Execute(Unit unit)
        {
            if (unit != executingUnit)
            {
                Debug.LogError("AttackCommand: Executing unit mismatch with instantiating unit!");
                return;
            }

            if (target == null || !target.IsAlive)
            {
                unit.ClearCurrentCommand();
                return;
            }

            var attackComp = unit.AttackComponent;
            var moveComp = unit.MoveComponent;

            if (attackComp == null)
            {
                Debug.LogError($"{unit.name} lacks AttackComponent for AttackCommand.");
                unit.ClearCurrentCommand();
                return;
            }

            if (attackComp.TryAttack(target))
            {
                // Attack successful. Command might persist if continuous attack is desired.
                // Unit's Update loop will re-evaluate or clear command based on target status.
            }
            else
            {
                // Could not attack (e.g., out of range, cooldown).
                var distanceToTarget = Vector2.Distance(unit.transform.position, target.MyTransform.position);
                if (distanceToTarget > attackComp.GetAttackRange())
                {
                    if (moveComp != null)
                    {
                        moveComp.MoveTo(target.MyTransform.position);
                    }
                    // else Debug.LogWarning($"{unit.name} cannot move to attack target (no MoveComponent)."); // Optional
                }
                else // In range, but couldn't attack (e.g. cooldown)
                {
                    if (moveComp != null)
                    {
                        moveComp.StopAndHoldPosition(); // Wait for cooldown, don't keep moving
                    }
                }
            }
        }

        public bool IsTargetStillValid()
        {
            return target != null && target.IsAlive;
        }
        
        public IDamageable GetTarget()
        {
            return target;
        }
    }
}