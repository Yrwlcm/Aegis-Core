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

        [SerializeField] Color markerColor = Color.cyan;
        [SerializeField] float markerSize = 0.18f;

        readonly List<Vector3> pathBuffer = new(); // переиспользуем без GC
        AIPath agent;
        bool visible;

        [Header("Colors")]
        [SerializeField] private Color defaultPathColor = Color.cyan; // Старый markerColor
        [SerializeField] private Color attackPathColor = Color.red;

        void Awake()
        {
            agent = GetComponent<AIPath>();

            line.positionCount = 0;
            line.startWidth = line.endWidth = width;
            // Устанавливаем дефолтный цвет при создании
            SetPathColor(defaultPathColor); 

            targetMarker.transform.localScale = Vector3.one * markerSize;
            // targetMarker.color = markerColor; // Цвет маркера будет меняться вместе с линией
            targetMarker.enabled = false;
        }

        void Update()
        {
            if (!visible || !agent.hasPath || agent.pathPending) return;

            pathBuffer.Clear();
            agent.GetRemainingPath(pathBuffer, out _);

            if (Vector3.Distance(agent.destination, pathBuffer.Last()) > 0.6f)
            {
                var startPoint = pathBuffer.First();
                pathBuffer.Clear();
                pathBuffer.Add(startPoint);
                pathBuffer.Add(agent.destination);
            }
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
            if (!state)
            {
                line.positionCount = 0;
                // При скрытии можно сбрасывать цвет на дефолтный, если команда отменилась
                SetPathColor(defaultPathColor); 
            }
        }

        public void SetPathColor(Color newColor)
        {
            // markerColor = newColor; // Обновляем текущий цвет, если он используется где-то еще
            line.startColor = line.endColor = newColor;
            if (targetMarker != null) // Проверка, если маркер опционален
            {
                targetMarker.color = newColor;
            }
        }

        // Метод для установки типа пути (и соответствующего цвета)
        public void SetPathMode(bool isAttackMode)
        {
            if (isAttackMode)
            {
                SetPathColor(attackPathColor);
            }
            else
            {
                SetPathColor(defaultPathColor);
            }
        }
    }
}