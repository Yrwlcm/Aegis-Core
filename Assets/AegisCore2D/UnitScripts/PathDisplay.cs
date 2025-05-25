using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(AIPath))]
    public sealed class PathDisplay : MonoBehaviour
    {
        [Header("Line")]
        [SerializeField] private LineRenderer line;
        [SerializeField] private float lineWidth = 0.04f;

        [Header("Target Marker")]
        [SerializeField] private SpriteRenderer targetMarker;
        [SerializeField] private float markerSize = 0.18f;

        private readonly List<Vector3> pathBuffer = new();
        private AIPath agent;
        private bool isVisible; // Renamed for clarity

        [Header("Colors")]
        [SerializeField] private Color defaultPathColor = Color.cyan;
        [SerializeField] private Color attackPathColor = Color.red;
        [SerializeField] private Color attackMovePathColor = Color.yellow;

        private IDamageable attackTargetOverride;
        public enum PathDisplayMode { None, Default, Attack, AttackMove }
        private PathDisplayMode currentDisplayMode = PathDisplayMode.None;

        private void Awake()
        {
            agent = GetComponent<AIPath>();

            if (line == null) { Debug.LogError("PathDisplay: LineRenderer not assigned.", this); enabled = false; return; }
            if (targetMarker == null) { Debug.LogError("PathDisplay: TargetMarker SpriteRenderer not assigned.", this); enabled = false; return; }


            line.positionCount = 0;
            line.startWidth = line.endWidth = lineWidth;
            line.enabled = false;

            targetMarker.transform.localScale = Vector3.one * markerSize;
            targetMarker.enabled = false;
            
            SetDisplayMode(PathDisplayMode.None); // Start with no display
        }

        public void SetAttackTargetOverride(IDamageable target)
        {
            this.attackTargetOverride = target;
        }

        private void Update()
        {
            if (!isVisible || currentDisplayMode == PathDisplayMode.None || agent == null)
            {
                if (line.enabled) line.enabled = false;
                if (targetMarker.enabled) targetMarker.enabled = false;
                return;
            }

            // Ensure enabled state is managed before drawing
            if (!line.enabled) line.enabled = true;
            if (!targetMarker.enabled) targetMarker.enabled = true;

            if (currentDisplayMode == PathDisplayMode.Attack && attackTargetOverride != null && attackTargetOverride.IsAlive)
            {
                DrawDirectLineToTarget(attackTargetOverride.MyTransform.position);
            }
            else if (currentDisplayMode == PathDisplayMode.Default || currentDisplayMode == PathDisplayMode.AttackMove)
            {
                DrawAgentPath();
            }
            else // Other modes or invalid state
            {
                if (line.enabled) line.enabled = false;
                if (targetMarker.enabled) targetMarker.enabled = false;
            }
        }
        
        private void DrawDirectLineToTarget(Vector3 targetPos)
        {
            line.positionCount = 2;
            line.SetPosition(0, transform.position); // Start from unit's current position
            line.SetPosition(1, targetPos);
            targetMarker.transform.position = targetPos;
        }

        private void DrawAgentPath()
        {
            var shouldDrawAiPath = agent.hasPath &&
                                   !agent.pathPending &&
                                   agent.remainingDistance > agent.endReachedDistance && // Use agent.remainingDistance
                                   agent.canMove;

            if (shouldDrawAiPath)
            {
                pathBuffer.Clear();
                agent.GetRemainingPath(pathBuffer, out _); // out bool stale - can be ignored if not used

                // A* might not return the exact agent.destination as the last point if it's unreachable/off-graph.
                // If the path is short or destination changed, ensure last point is the actual destination.
                if (pathBuffer.Count > 0 && Vector3.Distance(agent.destination, pathBuffer.Last()) > 0.6f) // Threshold for "close enough"
                {
                    var startPoint = pathBuffer.First(); // Keep the first calculated point
                    pathBuffer.Clear();
                    pathBuffer.Add(startPoint); // Could also use transform.position for current pos
                    pathBuffer.Add(agent.destination);
                }
                
                if (pathBuffer.Count > 0) // Path has at least one point
                {
                    // Prepend current unit position if not already the first point (for smoother line start)
                    if (pathBuffer.Count == 1 || Vector3.Distance(transform.position, pathBuffer.First()) > 0.1f)
                    {
                        pathBuffer.Insert(0, transform.position);
                    }

                    line.positionCount = pathBuffer.Count;
                    line.SetPositions(pathBuffer.ToArray()); // More efficient for setting multiple points
                    targetMarker.transform.position = agent.destination; // Marker always at final destination
                }
                else // No path from GetRemainingPath, but agent might still be trying to move to destination
                {
                     DrawDirectLineToTarget(agent.destination); // Fallback to direct line if path calculation fails but has dest
                }
            }
            else // Not moving or no path
            {
                if (line.enabled) line.enabled = false;
                if (targetMarker.enabled) targetMarker.enabled = false;
            }
        }


        public void SetVisible(bool state)
        {
            isVisible = state;
            if (!state) // Immediately hide if set to not visible
            {
                if (line.enabled) line.enabled = false;
                if (targetMarker.enabled) targetMarker.enabled = false;
                line.positionCount = 0;
            }
        }

        private void SetPathColor(Color newColor)
        {
            if (line != null) line.startColor = line.endColor = newColor;
            if (targetMarker != null) targetMarker.color = newColor;
        }

        public void SetDisplayMode(PathDisplayMode mode)
        {
            currentDisplayMode = mode;
            // Update visibility based on new mode, if path display is generally visible
            if (isVisible) 
            {
                if (mode == PathDisplayMode.None)
                {
                    if (line.enabled) line.enabled = false;
                    if (targetMarker.enabled) targetMarker.enabled = false;
                } else {
                    if (!line.enabled) line.enabled = true;
                    if (!targetMarker.enabled) targetMarker.enabled = true;
                }
            }


            switch (mode)
            {
                case PathDisplayMode.Default: SetPathColor(defaultPathColor); break;
                case PathDisplayMode.Attack: SetPathColor(attackPathColor); break;
                case PathDisplayMode.AttackMove: SetPathColor(attackMovePathColor); break;
                case PathDisplayMode.None:
                default:
                    // Color doesn't matter if line is disabled.
                    break;
            }
        }
    }
}