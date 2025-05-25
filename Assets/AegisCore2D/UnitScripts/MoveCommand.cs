using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public sealed class MoveCommand : IUnitCommand
    {
        private readonly Vector3 targetPosition;

        public MoveCommand(Vector3 target) => this.targetPosition = target;

        public void Execute(Unit unit)
        {
            var moveComp = unit.MoveComponent;
            if (moveComp != null)
            {
                moveComp.MoveTo(targetPosition);
                // The Unit's Update loop is responsible for checking if HasReachedDestination
                // and then calling ClearCurrentCommand.
            }
            else
            {
                // Debug.LogWarning($"{unit.name} cannot execute MoveCommand: Missing MoveComponent."); // Optional
                unit.ClearCurrentCommand(); // Cannot move, so command is effectively done/failed
            }
        }
        
        public Vector3 GetTargetPosition_DEBUG() 
        {
            return targetPosition;
        }
    }
}