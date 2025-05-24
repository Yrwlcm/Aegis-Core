using System.Collections.Generic;
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

        [SerializeField] Color markerColor = Color.cyan;
        [SerializeField] float markerSize = 0.18f;

        readonly List<Vector3> pathBuffer = new(); // переиспользуем без GC
        AIPath agent;
        bool visible;

        void Awake()
        {
            agent = GetComponent<AIPath>();

            line.positionCount = 0;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = markerColor;

            targetMarker.transform.localScale = Vector3.one * markerSize;
            targetMarker.color = markerColor;
            targetMarker.enabled = false;
        }

        void Update()
        {
            if (!visible || !agent.hasPath || agent.pathPending) return;

            pathBuffer.Clear();
            agent.GetRemainingPath(pathBuffer, out _);

            if (pathBuffer.Count == 0)
            {
                line.positionCount = 0;
                return;
            }

            int n = pathBuffer.Count;
            line.positionCount = n;

            for (int i = 0; i < n; i++)
            {
                Vector3 p = pathBuffer[i];
                line.SetPosition(i, p);
            }

            targetMarker.transform.position = agent.destination;
        }


        public void SetVisible(bool state)
        {
            visible = state;
            line.enabled = state;
            targetMarker.enabled = state;
            if (!state) line.positionCount = 0;
        }
    }
}