using Pathfinding;
using UnityEngine;

namespace AegisCore2D
{
    [RequireComponent(typeof(Seeker), typeof(AIPath))]
    public sealed class UnitMove : MonoBehaviour
    {
        private AIPath agent;

        private void Awake()
        {
            agent = GetComponent<AIPath>();
            agent.canMove = false;
        }

        public void MoveTo(Vector3 target)
        {
            agent.destination = target;
            agent.canMove     = true;
            agent.SearchPath();
        }

        public void Stop() => agent.canMove = false;
    }
}