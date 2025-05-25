// PathDisplay.cs

using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(AIPath))]
    public sealed class PathDisplay : MonoBehaviour
    {
        [Header("Line")] [SerializeField] LineRenderer line;
        [SerializeField] float width = 0.04f;

        [Header("Target Marker")] [SerializeField]
        SpriteRenderer targetMarker;

        [SerializeField] Color markerColor = Color.cyan; // This will be overridden by SetPathColor
        [SerializeField] float markerSize = 0.18f;

        readonly List<Vector3> pathBuffer = new();
        AIPath agent;
        bool visible;

        [Header("Colors")]
        [SerializeField] private Color defaultPathColor = Color.cyan;
        [SerializeField] private Color attackPathColor = Color.red;

        private IDamageable attackTargetOverride; // To store the specific attack target

        void Awake()
        {
            agent = GetComponent<AIPath>();

            line.positionCount = 0;
            line.startWidth = line.endWidth = width;
            SetPathColor(defaultPathColor); // Initialize with default color

            targetMarker.transform.localScale = Vector3.one * markerSize;
            targetMarker.enabled = false;
        }

        public void SetAttackTargetOverride(IDamageable target)
        {
            this.attackTargetOverride = target;
        }

        void Update()
        {
            if (!visible)
            {
                line.enabled = false;
                targetMarker.enabled = false;
                return;
            }

            if (attackTargetOverride != null && attackTargetOverride.IsAlive)
            {
                // ATTACK TARGET OVERRIDE MODE: Draw a direct line to the attack target.
                // Color should have been set to red by Unit.cs calling SetPathMode(true).
                line.positionCount = 2;
                line.SetPosition(0, transform.position); // Unit's current position
                line.SetPosition(1, attackTargetOverride.MyTransform.position);
                line.enabled = true;

                targetMarker.transform.position = attackTargetOverride.MyTransform.position;
                targetMarker.enabled = true;
            }
            else
            {
                // NORMAL MOVEMENT PATH MODE (or no specific path to draw for current command)
                // Color should have been set to default by Unit.cs calling SetPathMode(false).

                bool shouldDrawAiPath = agent.hasPath &&
                                        !agent.pathPending &&
                                        agent.remainingDistance > agent.endReachedDistance &&
                                        agent.canMove; // Check if agent is actively trying to move along a path

                if (shouldDrawAiPath)
                {
                    pathBuffer.Clear();
                    agent.GetRemainingPath(pathBuffer, out _);

                    // Original logic for handling paths that don't seem to reach the agent's final destination
                    if (pathBuffer.Count > 0 && Vector3.Distance(agent.destination, pathBuffer.Last()) > 0.6f)
                    {
                        var startPoint = pathBuffer.First();
                        pathBuffer.Clear();
                        pathBuffer.Add(startPoint);
                        pathBuffer.Add(agent.destination);
                    }

                    if (pathBuffer.Count > 0)
                    {
                        line.positionCount = pathBuffer.Count;
                        for (int i = 0; i < pathBuffer.Count; i++)
                        {
                            line.SetPosition(i, pathBuffer[i]);
                        }
                        line.enabled = true;
                        targetMarker.transform.position = agent.destination; // Marker at A* path destination
                        targetMarker.enabled = true;
                    }
                    else // No points from GetRemainingPath, but conditions for shouldDrawAiPath were met
                    {
                        // This case is less common if shouldDrawAiPath is true.
                        // Could happen if path is extremely short or just became valid/invalid.
                        // Fallback to a direct line if still moving towards a distinct destination.
                        if (Vector3.Distance(transform.position, agent.destination) > agent.endReachedDistance && agent.canMove)
                        {
                            line.positionCount = 2;
                            line.SetPosition(0, transform.position);
                            line.SetPosition(1, agent.destination);
                            line.enabled = true;
                            targetMarker.transform.position = agent.destination;
                            targetMarker.enabled = true;
                        }
                        else
                        {
                            line.enabled = false;
                            targetMarker.enabled = false;
                        }
                    }
                }
                else // No A* path to draw (e.g., at destination, no path, path pending, or agent.canMove is false)
                {
                    line.enabled = false;
                    targetMarker.enabled = false;
                }
            }
        }

        public void SetVisible(bool state)
        {
            visible = state;
            // Enabling/disabling of line and marker is now handled in Update based on conditions
            if (!state) // If explicitly hiding, turn them off immediately
            {
                line.enabled = false;
                targetMarker.enabled = false;
                line.positionCount = 0;
                // Removed SetPathColor(defaultPathColor) to let Unit manage color persistence
            }
        }

        public void SetPathColor(Color newColor)
        {
            line.startColor = line.endColor = newColor;
            if (targetMarker != null)
            {
                targetMarker.color = newColor;
            }
        }

        public void SetPathMode(bool isAttackMode)
        {
            SetPathColor(isAttackMode ? attackPathColor : defaultPathColor);
        }
    }
}