using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public sealed class MoveCommand : IUnitCommand
    {
        private readonly Vector3 targetPosition;
        // private bool reached = false; // Флаг, что достигли (если команда должна сама себя завершать)

        public MoveCommand(Vector3 target) => this.targetPosition = target;

        public void Execute(Unit unit)
        {
            // if (reached) return; // Если уже достигли, ничего не делаем

            UnitMove moveComp = unit.MoveComponent;
            if (moveComp != null)
            {
                moveComp.MoveTo(targetPosition);
                // Логика завершения команды теперь в Unit.Update()
                // if (moveComp.HasReachedDestination())
                // {
                //    // Debug.Log($"{unit.name} достиг цели в MoveCommand.Execute.");
                //    // unit.ClearCurrentCommand(); // Сообщаем юниту, что команда выполнена
                //    // reached = true;
                // }
            }
        }
        
        // Метод для отладки или для проверки в Unit.SetCommand
        public Vector3 GetTargetPosition_DEBUG() 
        {
            return targetPosition;
        }
    }
}