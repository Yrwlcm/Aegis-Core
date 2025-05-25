using Pathfinding;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(Seeker), typeof(AIPath))]
    public sealed class UnitMove : MonoBehaviour
    {
        // AIPath component reference, public for read-only access by other systems (like PathDisplay)
        // but primarily controlled by this UnitMove class.
        public AIPath agent { get; private set; }


        private void Awake()
        {
            agent = GetComponent<AIPath>();
            if (agent == null)
            {
                Debug.LogError("UnitMove requires an AIPath component!", this);
                enabled = false;
                return;
            }
            agent.canMove = false; // Start stationary
        }
        
        /// <summary>
        /// Stops movement and path searching, holding current position.
        /// </summary>
        public void StopAndHoldPosition() 
        {
            if (agent == null) return;
            agent.canMove = false;
            agent.canSearch = false; 
            agent.destination = transform.position;
            agent.SetPath(null); // Clear current path to stop immediately
            agent.FinalizeMovement(transform.position, agent.rotation); // Reset velocity
        }
        
        /// <summary>
        /// Allows the agent to move and search for paths again.
        /// </summary>
        public void AllowMovementAndSearch()
        {
            if (agent == null) return;
            agent.canMove = true;
            agent.canSearch = true;
        }

        /// <summary>
        /// Commands the unit to move to the target position.
        /// </summary>
        public void MoveTo(Vector3 target)
        {
            if (agent == null) return;
            
            AllowMovementAndSearch(); // Ensure agent can move before setting destination

            // Avoid re-pathing if already moving to a very close target
            // and path is still valid. This threshold can be tuned.
            if (agent.hasPath && agent.pathPending == false &&
                Vector3.Distance(agent.destination, target) < 0.1f &&
                agent.remainingDistance > agent.endReachedDistance) // Still has distance to cover
            {
                return;
            }

            agent.destination = target;
            // AIPath should automatically search path if canSearch is true and destination changes.
            // Explicitly call if issues:
            // if (agent.isActiveAndEnabled) agent.SearchPath();
        }

        /// <summary>
        /// Stops movement immediately.
        /// </summary>
        public void Stop()
        {
            if (agent == null) return;
            agent.canMove = false;
            // agent.destination = transform.position; // Setting destination might make it recalculate a tiny path.
            agent.SetPath(null); // Clear current path more effectively.
            agent.FinalizeMovement(transform.position, agent.rotation); // Reset velocity etc.
        }

        /// <summary>
        /// Checks if the unit is currently actively moving towards a destination.
        /// </summary>
        public bool IsMoving()
        {
            if (agent == null) return false;
            // Check if agent can move, has a path, and is not yet at the end of it.
            // AIPath.desiredVelocity.sqrMagnitude > 0.01f is also a good indicator.
            return agent.canMove && agent.hasPath && agent.remainingDistance > agent.endReachedDistance;
        }

        /// <summary>
        /// Checks if the unit has reached its current destination.
        /// </summary>
        public bool HasReachedDestination()
        {
            if (agent == null) return true; // If no agent, arguably "at destination"
            // reachedEndOfPath is true when remainingDistance <= endReachedDistance.
            // Also check not pathPending to ensure it's not about to start a new path.
            return !agent.pathPending && agent.reachedEndOfPath;
        }
        
        /// <summary>
        /// Gets the current destination of the agent.
        /// </summary>
        public Vector3 GetDestination()
        {
            return agent != null ? agent.destination : transform.position;
        }
    }
}