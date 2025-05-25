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

        [Header("References")]
        [SerializeField] private Camera cam;
        [SerializeField] private RectTransform boxVisual;

        [Header("Filters")]
        [SerializeField] private LayerMask selectableMask;
        [SerializeField] private LayerMask attackableMask; // Used by AttackMoveCommand via GetAttackableMask_DEBUG
        [SerializeField] private int teamId;
        [SerializeField] private bool isPlayerManager;

        private readonly HashSet<ISelectable> pool = new();
        private readonly HashSet<ISelectable> dragBuffer = new();
        private readonly HashSet<Unit> selected = new();

        private Rect dragRect;
        private Vector2 dragStart;
        private bool dragging = false;

        private const float DragThreshold = 40f; // Renamed for convention

        private InputSystem_Actions inputActions;
        private bool attackMoveModeActive = false;

        private void Awake()
        {
            if (Instances.TryGetValue(teamId, out var inst) && inst != this)
            {
                Destroy(gameObject);
                return;
            }
            Instances[teamId] = this;

            inputActions = new InputSystem_Actions();

            if (boxVisual != null)
            {
                boxVisual.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("BoxVisual (RectTransform for selection box) is not assigned in SelectionManager.", this);
            }
        }

        private void OnEnable()
        {
            inputActions?.Enable();
            if (inputActions != null && inputActions.Player.AttackMoveModifier != null)
            {
                inputActions.Player.AttackMoveModifier.performed += OnAttackMoveModifierPerformed;
            }
        }

        private void OnDisable()
        {
            inputActions?.Disable();
            if (inputActions != null && inputActions.Player.AttackMoveModifier != null)
            {
                inputActions.Player.AttackMoveModifier.performed -= OnAttackMoveModifierPerformed;
            }
        }

        private void OnAttackMoveModifierPerformed(InputAction.CallbackContext context)
        {
            if (selected.Count == 0 && !attackMoveModeActive)
            {
                // Debug.Log("Attack-Move Mode: Cannot activate, no units selected."); // Optional log
                return;
            }

            attackMoveModeActive = !attackMoveModeActive;
            // Debug.Log("Attack-Move Mode: " + (attackMoveModeActive ? "ON" : "OFF") + " (Toggled by key)"); // Optional log
        }

        public LayerMask GetAttackableMask_DEBUG() // Keep for AttackMoveCommand dependency
        {
            return attackableMask;
        }

        private void Update()
        {
            if (!isPlayerManager) return;
            
            HandleLeftMouseInput();
            HandleRightMouseInput();
        }

        private void HandleLeftMouseInput()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BeginDrag();
            }
            else if (Mouse.current.leftButton.isPressed)
            {
                if (dragging) UpdateDrag();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (dragging) EndDrag();
                // else if (attackMoveModeActive) // If LMB release while A-move active, maybe cancel A-move mode?
                // {
                //    attackMoveModeActive = false;
                //    Debug.Log("Attack-Move Mode: OFF (LMB release)");
                // }
            }
        }

        private void HandleRightMouseInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (selected.Count == 0) return;

                var screen = Mouse.current.position.ReadValue();
                var world = cam.ScreenToWorldPoint(screen);

                if (attackMoveModeActive)
                {
                    // Debug.Log($"Issuing ATTACK-MOVE command to {world}"); // Optional log
                    BroadcastAttackMove(world);
                    attackMoveModeActive = false; 
                    // Debug.Log("Attack-Move Mode: OFF (Command issued)"); // Optional log
                }
                else
                {
                    var hitAttackable = Physics2D.OverlapPoint(world, attackableMask);
                    if (hitAttackable != null)
                    {
                        var damageableTarget = hitAttackable.GetComponentInSelfOrParent<IDamageable>();

                        if (damageableTarget != null && damageableTarget.IsAlive)
                        {
                            var isOwnTeamTarget = false;
                            var firstSelectedUnit = selected.FirstOrDefault();
                            if (firstSelectedUnit != null && firstSelectedUnit.Health != null &&
                                firstSelectedUnit.Team == damageableTarget.TeamId && damageableTarget.TeamId != -1)
                            {
                                isOwnTeamTarget = true;
                            }

                            if (!isOwnTeamTarget)
                            {
                                // Debug.Log($"Commanding selected units to ATTACK {damageableTarget.MyGameObject.name}"); // Optional log
                                BroadcastAttack(damageableTarget);
                                return;
                            }
                        }
                    }
                    // Debug.Log($"Commanding selected units to MOVE to {world}"); // Optional log
                    BroadcastMove(world);
                }
            }
        }

        private void BeginDrag()
        {
            if (attackMoveModeActive)
            {
                // Potentially cancel attack move mode on LMB click? Or handle A+Click for a specific target
                // For now, LMB during attack-move doesn't start selection drag
                return; 
            }

            if (!Keyboard.current.leftShiftKey.isPressed)
            {
                DeselectAll();
            }

            dragStart = Mouse.current.position.ReadValue();
            dragging = true;

            if (boxVisual != null)
            {
                boxVisual.gameObject.SetActive(true);
                boxVisual.anchoredPosition = dragStart;
                boxVisual.sizeDelta = Vector2.zero;
            }
            dragRect = new Rect(dragStart, Vector2.zero);
            dragBuffer.Clear();
        }

        private void UpdateDrag()
        {
            var cur = Mouse.current.position.ReadValue();
            var min = Vector2.Min(dragStart, cur);
            var max = Vector2.Max(dragStart, cur);

            if (boxVisual != null)
            {
                boxVisual.anchoredPosition = min;
                boxVisual.sizeDelta = max - min;
            }

            dragRect.min = min;
            dragRect.max = max;

            foreach (var sel in pool)
            {
                if (sel == null || sel.GameObject == null) continue;
                var scrPos = cam.WorldToScreenPoint(sel.GameObject.transform.position);
                if (dragRect.Contains(scrPos))
                {
                    if (sel.Team == teamId) // Only select own team units
                    {
                        if (dragBuffer.Add(sel)) // Add returns true if item was added
                        {
                            sel.EnableOutline();
                        }
                    }
                }
                else 
                {
                    if (dragBuffer.Remove(sel)) // Remove returns true if item was removed
                    {
                        var unitSel = sel as Unit;
                        if (unitSel == null || !selected.Contains(unitSel))
                        {
                            sel.DisableOutline();
                        }
                    }
                }
            }
        }

        private void EndDrag()
        {
            if (boxVisual != null)
            {
                boxVisual.gameObject.SetActive(false);
            }

            if (Vector2.Distance(dragStart, Mouse.current.position.ReadValue()) < DragThreshold) 
            {
                ClickSelect();
            }
            else 
            {
                BoxSelect();
            }
            
            // Clean up outlines from items that were in dragBuffer but didn't make it to final 'selected'
            // This loop might be redundant if BoxSelect and DeselectAll correctly manage outlines.
            // However, keeping it for safety to ensure no dangling outlines from dragBuffer.
            foreach (var item in pool) 
            {
                if (item != null && !dragBuffer.Contains(item) && item.OutlineEnabled)
                {
                     var unitItem = item as Unit;
                     if (unitItem == null || !selected.Contains(unitItem))
                     {
                        item.DisableOutline();
                     }
                }
            }
            dragBuffer.Clear();
            dragging = false;
        }
        
        public void CancelDrag() // Public if called from elsewhere, e.g. UI or other input
        {
            if (!dragging) return;

            if (boxVisual != null)
            {
                boxVisual.gameObject.SetActive(false);
            }
            
            foreach (var item in dragBuffer)
            {
                if (item != null)
                {
                    var unitItem = item as Unit;
                    if (unitItem == null || !selected.Contains(unitItem)) 
                    {
                        item.DisableOutline();
                    }
                }
            }
            dragBuffer.Clear();
            dragging = false;
            // Debug.Log("Drag Canceled"); // Optional log
        }

        private void ClickSelect()
        {
            if (attackMoveModeActive) return;

            var screen = Mouse.current.position.ReadValue();
            var world = cam.ScreenToWorldPoint(screen);
            var hit = Physics2D.OverlapPoint(world, selectableMask);

            var shiftPressed = Keyboard.current.leftShiftKey.isPressed;

            if (hit != null)
            {
                var sel = hit.GetComponent<ISelectable>();
                if (sel != null && sel.Team == teamId) // Can only select own team units
                {
                    ToggleSelect(sel as Unit, shiftPressed);
                    return; 
                }
            }

            if (!shiftPressed)
            {
                DeselectAll();
            }
        }

        private void BoxSelect()
        {
            var shiftPressed = Keyboard.current.leftShiftKey.isPressed;
            if (!shiftPressed) 
            {
                DeselectAll();
            }

            foreach (var s in dragBuffer) 
            {
                Select(s as Unit); 
            }
            // dragBuffer is cleared in EndDrag
        }

        private void ToggleSelect(Unit u, bool shiftIsPressed)
        {
            if (u == null || u.Health == null || !u.Health.IsAlive) return;

            if (shiftIsPressed)
            {
                if (selected.Contains(u)) Deselect(u);
                else Select(u);
            }
            else 
            {
                DeselectAll();
                Select(u);
            }
        }

        private void Select(params Unit[] units)
        {
            foreach (var unit in units)
            {
                if (unit != null && unit.Health != null && unit.Health.IsAlive)
                {
                    if (selected.Add(unit)) 
                    {
                        unit.Select(); 
                    }
                }
            }
        }

        private void Deselect(params Unit[] units)
        {
            foreach (var unit in units)
            {
                if (unit != null)
                {
                    if (selected.Remove(unit)) 
                    {
                        unit.Deselect(); 
                    }
                }
            }
        }

        private void DeselectAll()
        {
            var currentlySelected = selected.ToArray(); // ToArray avoids collection modification during iteration
            foreach (var u in currentlySelected)
            {
                Deselect(u); 
            }
            // selected set should be empty after Deselect(u) calls if u was in selected.
            // If selected.Remove(u) in Deselect didn't clear it, an explicit selected.Clear() might be needed,
            // but current logic implies it will be cleared.
        }

        private void BroadcastAttackMove(Vector2 targetPoint)
        {
            foreach (var u in selected)
            {
                if (u == null || u.AttackComponent == null) continue;
                u.SetCommand(new AttackMoveCommand(targetPoint));
            }
        }

        private void BroadcastMove(Vector2 target)
        {
            foreach (var u in selected)
            {
                if (u == null) continue;
                u.SetCommand(new MoveCommand(target));
            }
        }

        private void BroadcastAttack(IDamageable target)
        {
            foreach (var u in selected)
            {
                if (u == null) continue;
                u.SetCommand(new AttackCommand(u, target));
            }
        }

        public static void RegisterUnitForTeam(ISelectable unit, int team)
        {
            if (!Instances.TryGetValue(team, out var mgr)) return;
            if (unit != null) mgr.pool.Add(unit);
        }

        public static void RemoveUnitForTeam(ISelectable unit, int team)
        {
            if (!Instances.TryGetValue(team, out var mgr) || unit == null) return;
            mgr.pool.Remove(unit);
            if (unit is Unit u)
            {
                mgr.selected.Remove(u); // Ensure unit is removed from selection if it dies/is removed
            }
            mgr.dragBuffer.Remove(unit); // Also from drag buffer
        }
    }
}