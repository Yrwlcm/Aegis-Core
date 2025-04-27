using UnityEngine;

namespace AegisCore2D
{
    public sealed class MoveCommand : IUnitCommand
    {
        private readonly Vector3 target;
        public MoveCommand(Vector3 target) => this.target = target;

        public void Execute(Unit unit) => unit.MoveComponent.MoveTo(target);
    }
}