using System;
using System.Collections.Generic;
using System.Linq;
using AegisCore2D.UnitScripts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AegisCore2D.GeneralScripts
{
    public sealed class SelectionManager : MonoBehaviour
    {
        public static Dictionary<int, SelectionManager> Instances { get; } = new();

        [Header("References")] [SerializeField]
        Camera cam;

        [SerializeField] RectTransform boxVisual;

        [Header("Filters")] [SerializeField] LayerMask selectableMask; // союзные юниты
        [SerializeField] LayerMask attackableMask; // враги/объекты
        [SerializeField] int teamId;
        [SerializeField] bool isPlayerManager;

        readonly HashSet<ISelectable> pool = new(); // все доступные юниты
        readonly HashSet<ISelectable> dragBuffer = new();
        readonly HashSet<Unit> selected = new();

        Rect dragRect;
        Vector2 dragStart;
        bool dragging;

        const float dragThreshold = 40f;

        /* ─── singleton ─── */
        void Awake()
        {
            if (Instances.TryGetValue(teamId, out var inst) && inst != this)
                Destroy(gameObject);
            else
                Instances[teamId] = this;
        }

        /* ─── main loop ─── */
        void Update()
        {
            if (!isPlayerManager) return;

            HandleSelectionInput();
            HandleCommandInput();
        }

        /* ─── selection input ─── */
        void HandleSelectionInput()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) BeginDrag();
            if (Mouse.current.leftButton.isPressed && dragging) UpdateDrag();
            if (Mouse.current.leftButton.wasReleasedThisFrame) EndDrag();
        }

        void BeginDrag()
        {
            if (!Keyboard.current.leftShiftKey.isPressed)
                DeselectAll();

            dragStart = Mouse.current.position.ReadValue();
            dragging = true;

            boxVisual.gameObject.SetActive(true);
            boxVisual.sizeDelta = Vector2.zero;
            dragRect = new Rect();
        }

        void UpdateDrag()
        {
            Vector2 cur = Mouse.current.position.ReadValue();
            Vector2 min = Vector2.Min(dragStart, cur);
            Vector2 max = Vector2.Max(dragStart, cur);

            boxVisual.anchoredPosition = min;
            boxVisual.sizeDelta = max - min;

            dragRect.min = min;
            dragRect.max = max;

            foreach (var sel in pool)
            {
                Vector2 scr = cam.WorldToScreenPoint(sel.GameObject.transform.position);
                if (dragRect.Contains(scr))
                {
                    if (dragBuffer.Add(sel)) sel.EnableOutline();
                }
                else if (dragBuffer.Remove(sel) && !selected.Contains((Unit)sel))
                {
                    sel.DisableOutline();
                }
            }
        }

        void EndDrag()
        {
            dragging = false;
            boxVisual.gameObject.SetActive(false);

            if (boxVisual.sizeDelta.magnitude < dragThreshold)
                ClickSelect();
            else
                BoxSelect();

            dragBuffer.Clear();
        }

        void ClickSelect()
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(screen);
            Collider2D hit = Physics2D.OverlapPoint(world, selectableMask);
            if (!hit) return;

            var sel = hit.GetComponent<ISelectable>();
            if (sel?.Team != teamId) return;

            ToggleSelect((Unit)sel);
        }

        void BoxSelect()
        {
            foreach (var s in dragBuffer) Select((Unit)s);
        }

        /* ─── command input ─── */
        void HandleCommandInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (selected.Count == 0) return;

                Vector2 screen = Mouse.current.position.ReadValue();
                Vector2 world = cam.ScreenToWorldPoint(screen);

                BroadcastMove(world,
                    (target, _, _) => target); // FormationUtility.GetSpiralOffset(index, spacing: 0.8f)
            }
        }

        void BroadcastMove(Vector2 target, Func<Vector2, Unit, int, Vector2> modifyTarget)
        {
            var index = 0;
            foreach (Unit u in selected)
                u.Enqueue(new MoveCommand(modifyTarget(target, u, index++)));
        }

        void Broadcast(IUnitCommand cmd)
        {
            foreach (Unit u in selected) u.Enqueue(cmd);
        }

        /* ─── selection utilities ─── */
        void ToggleSelect(Unit u)
        {
            if (selected.Contains(u)) Deselect(u);
            else Select(u);
        }

        void Select(params Unit[] units)
        {
            foreach (var unit in units)
            {
                unit.Select();
                unit.GetComponent<PathDisplay>()?.SetVisible(true);
            }

            selected.UnionWith(units);
        }

        void Deselect(params Unit[] units)
        {
            foreach (var unit in units)
            {
                unit.Deselect();
                unit.GetComponent<PathDisplay>()?.SetVisible(false);
            }

            selected.ExceptWith(units);
        }

        void DeselectAll()
        {
            Deselect(selected.ToArray());
        }

        public static void RegisterUnitForTeam(ISelectable unit, int team)
        {
            if (!Instances.TryGetValue(team, out var mgr)) return;
            mgr.pool.Add(unit);
        }

        public static void RemoveUnitForTeam(ISelectable unit, int team)
        {
            if (!Instances.TryGetValue(team, out var mgr)) return;
            mgr.pool.Remove(unit);
            mgr.selected.Remove((Unit)unit);
            mgr.dragBuffer.Remove(unit);
        }
    }
}