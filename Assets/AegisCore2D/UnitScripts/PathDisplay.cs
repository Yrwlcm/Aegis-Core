// --- START OF FILE PathDisplay.cs ---
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
        [SerializeField] LineRenderer line;
        [SerializeField] float width = 0.04f;

        [Header("Target Marker")]
        [SerializeField] SpriteRenderer targetMarker;
        [SerializeField] float markerSize = 0.18f;

        readonly List<Vector3> pathBuffer = new();
        AIPath agent;
        bool visible;

        [Header("Colors")]
        [SerializeField] private Color defaultPathColor = Color.cyan;
        [SerializeField] private Color attackPathColor = Color.red;
        [SerializeField] private Color attackMovePathColor = Color.yellow;

        private IDamageable attackTargetOverride;
        public enum PathDisplayMode { None, Default, Attack, AttackMove }
        private PathDisplayMode currentDisplayMode = PathDisplayMode.None;

        void Awake()
        {
            agent = GetComponent<AIPath>();

            line.positionCount = 0;
            line.startWidth = line.endWidth = width;

            targetMarker.transform.localScale = Vector3.one * markerSize;
            targetMarker.enabled = false;
            SetDisplayMode(PathDisplayMode.Default); // Initialize with a default mode
        }

        public void SetAttackTargetOverride(IDamageable target)
        {
            this.attackTargetOverride = target;
        }

        void Update()
        {
            if (!visible || currentDisplayMode == PathDisplayMode.None)
            {
                line.enabled = false;
                targetMarker.enabled = false;
                return;
            }

            line.enabled = true;
            targetMarker.enabled = true;

            if (currentDisplayMode == PathDisplayMode.Attack && attackTargetOverride != null && attackTargetOverride.IsAlive)
            {
                line.positionCount = 2;
                line.SetPosition(0, transform.position);
                line.SetPosition(1, attackTargetOverride.MyTransform.position);
                targetMarker.transform.position = attackTargetOverride.MyTransform.position;
            }
            else if (currentDisplayMode == PathDisplayMode.Default || currentDisplayMode == PathDisplayMode.AttackMove)
            {
                bool shouldDrawAiPath = agent.hasPath &&
                                        !agent.pathPending &&
                                        agent.remainingDistance > agent.endReachedDistance &&
                                        agent.canMove;

                if (shouldDrawAiPath)
                {
                    pathBuffer.Clear();
                    agent.GetRemainingPath(pathBuffer, out _);

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
                        targetMarker.transform.position = agent.destination;
                    }
                    else
                    {
                        if (Vector3.Distance(transform.position, agent.destination) > agent.endReachedDistance && agent.canMove)
                        {
                            line.positionCount = 2;
                            line.SetPosition(0, transform.position);
                            line.SetPosition(1, agent.destination);
                            targetMarker.transform.position = agent.destination;
                        }
                        else
                        {
                             line.enabled = false;
                             targetMarker.enabled = false;
                        }
                    }
                }
                else
                {
                    line.enabled = false;
                    targetMarker.enabled = false;
                }
            }
            else
            {
                line.enabled = false;
                targetMarker.enabled = false;
            }
        }

        public void SetVisible(bool state)
        {
            visible = state;
            if (!state)
            {
                line.enabled = false;
                targetMarker.enabled = false;
                line.positionCount = 0;
            }
        }

        private void SetPathColor(Color newColor)
        {
            line.startColor = line.endColor = newColor;
            if (targetMarker != null)
            {
                targetMarker.color = newColor;
            }
        }

        public void SetDisplayMode(PathDisplayMode mode)
        {
            currentDisplayMode = mode;
            switch (mode)
            {
                case PathDisplayMode.Default:
                    SetPathColor(defaultPathColor);
                    break;
                case PathDisplayMode.Attack:
                    SetPathColor(attackPathColor);
                    break;
                case PathDisplayMode.AttackMove:
                    SetPathColor(attackMovePathColor);
                    break;
                case PathDisplayMode.None:
                default:
                    // Color doesn't matter if line is disabled by Update logic
                    break;
            }
        }
    }
}
// --- END OF FILE PathDisplay.cs ---