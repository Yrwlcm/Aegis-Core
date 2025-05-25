// --- START OF FILE SelectionManager.cs ---
using System;
using System.Collections.Generic;
using System.Linq;
using AegisCore2D.UnitScripts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace AegisCore2D.GeneralScripts
{
    public sealed class SelectionManager : MonoBehaviour
    {
        public static Dictionary<int, SelectionManager> Instances { get; } = new();

        [Header("References")]
        [SerializeField] Camera cam;
        [SerializeField] RectTransform boxVisual;

        [Header("Filters")]
        [SerializeField] LayerMask selectableMask;
        [SerializeField] LayerMask attackableMask;
        [SerializeField] int teamId;
        [SerializeField] bool isPlayerManager;

        readonly HashSet<ISelectable> pool = new();
        readonly HashSet<ISelectable> dragBuffer = new();
        readonly HashSet<Unit> selected = new();

        Rect dragRect;
        Vector2 dragStart;
        bool dragging = false; // Явно инициализируем false

        const float dragThreshold = 40f;

        private InputSystem_Actions inputActions;
        private bool attackMoveModeActive = false;

        void Awake()
        {
            if (Instances.TryGetValue(teamId, out var inst) && inst != this)
            {
                Destroy(gameObject);
                return;
            }
            Instances[teamId] = this;

            inputActions = new InputSystem_Actions();

            if (boxVisual != null) // Убедимся, что boxVisual скрыт при старте
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
            inputActions?.Enable(); // Включаем весь asset, а не только Player map, если есть другие карты
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
            // Не включать режим, если никто не выбран и мы пытаемся его *включить*
            if (selected.Count == 0 && !attackMoveModeActive)
            {
                 Debug.Log("Attack-Move Mode: Cannot activate, no units selected.");
                return;
            }

            attackMoveModeActive = !attackMoveModeActive;
            Debug.Log("Attack-Move Mode: " + (attackMoveModeActive ? "ON" : "OFF") + " (Toggled by key)");

            if (!attackMoveModeActive)
            {
                // Если режим был выключен клавишей 'A' (а не ПКМ),
                // и у юнитов была команда AttackMove, ее можно отменить
                // или оставить - зависит от желаемого поведения.
                // Пока оставим как есть, чтобы не отменять команду, если пользователь просто передумал.
            }
        }

        public LayerMask GetAttackableMask_DEBUG()
        {
            return attackableMask;
        }

        void Update()
        {
            if (!isPlayerManager) return;
            
            HandleLeftMouseInput();
            HandleRightMouseInput();
        }

        void HandleLeftMouseInput()
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
            }
        }

        void HandleRightMouseInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (selected.Count == 0) return;

                Vector2 screen = Mouse.current.position.ReadValue();
                Vector2 world = cam.ScreenToWorldPoint(screen);

                if (attackMoveModeActive)
                {
                    Debug.Log($"Issuing ATTACK-MOVE command to {world}");
                    BroadcastAttackMove(world);
                    attackMoveModeActive = false; // Сбрасываем режим после команды
                    Debug.Log("Attack-Move Mode: OFF (Command issued)");
                }
                else
                {
                    Collider2D hitAttackable = Physics2D.OverlapPoint(world, attackableMask);
                    if (hitAttackable != null)
                    {
                        IDamageable damageableTarget = hitAttackable.GetComponent<IDamageable>();
                        if (damageableTarget == null) damageableTarget = hitAttackable.GetComponentInParent<IDamageable>();

                        if (damageableTarget != null && damageableTarget.IsAlive)
                        {
                            bool isOwnTeamTarget = false;
                            Unit firstSelectedUnit = selected.FirstOrDefault();
                            if (firstSelectedUnit != null && firstSelectedUnit.Health != null &&
                                firstSelectedUnit.Team == damageableTarget.TeamId && damageableTarget.TeamId != -1)
                            {
                                isOwnTeamTarget = true;
                            }

                            if (!isOwnTeamTarget)
                            {
                                Debug.Log($"Commanding selected units to ATTACK {damageableTarget.MyGameObject.name}");
                                BroadcastAttack(damageableTarget);
                                return;
                            }
                        }
                    }
                    Debug.Log($"Commanding selected units to MOVE to {world}");
                    BroadcastMove(world);
                }
            }
        }


        void BeginDrag()
        {
            // Если режим Attack-Move активен, ЛКМ не должен начинать выделение.
            // Он может быть использован для других целей или просто игнорироваться.
            // Сейчас мы его просто игнорируем для выделения.
            if (attackMoveModeActive)
            {
                // Можно добавить логику, если A+ЛКМ что-то делает, или просто выйти.
                // Пока выходим, чтобы не мешать.
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
                boxVisual.anchoredPosition = dragStart; // Начинаем с точки клика
                boxVisual.sizeDelta = Vector2.zero;
            }
            dragRect = new Rect(dragStart, Vector2.zero); // Инициализируем Rect
            dragBuffer.Clear(); // Очищаем буфер перед новым выделением
        }

        void UpdateDrag()
        {
            Vector2 cur = Mouse.current.position.ReadValue();
            Vector2 min = Vector2.Min(dragStart, cur);
            Vector2 max = Vector2.Max(dragStart, cur);

            if (boxVisual != null)
            {
                boxVisual.anchoredPosition = min;
                boxVisual.sizeDelta = max - min;
            }

            dragRect.min = min;
            dragRect.max = max;

            // Обновление dragBuffer
            // Чтобы избежать многократного Add/Remove, можно сделать так:
            // 1. Очистить dragBuffer от тех, кто больше не выделен
            // 2. Добавить новых
            // Но для простоты пока оставим старый подход, он не должен быть причиной зависания.

            foreach (var sel in pool)
            {
                if (sel == null || sel.GameObject == null) continue;
                Vector2 scrPos = cam.WorldToScreenPoint(sel.GameObject.transform.position);
                if (dragRect.Contains(scrPos))
                {
                    if (sel.Team == teamId)
                    {
                        if (!dragBuffer.Contains(sel)) // Добавляем, только если еще нет
                        {
                            dragBuffer.Add(sel);
                            sel.EnableOutline();
                        }
                    }
                }
                else // Юнит не в прямоугольнике
                {
                    if (dragBuffer.Contains(sel)) // Если был в буфере, удаляем
                    {
                        dragBuffer.Remove(sel);
                        // Снимаем аутлайн, только если он не в основном списке 'selected'
                        Unit unitSel = sel as Unit;
                        if (unitSel == null || !selected.Contains(unitSel))
                        {
                            sel.DisableOutline();
                        }
                    }
                }
            }
        }

        void EndDrag()
        {
            if (boxVisual != null)
            {
                boxVisual.gameObject.SetActive(false);
            }

            if (Vector2.Distance(dragStart, Mouse.current.position.ReadValue()) < dragThreshold) // Проверка на "клик"
            {
                ClickSelect();
            }
            else // Это было выделение рамкой
            {
                BoxSelect();
            }

            // Очистка после выделения
            foreach (var item in pool) // Снимаем подсветку с тех, кто был в dragBuffer, но не попал в selected
            {
                if (item != null && !dragBuffer.Contains(item) && item.OutlineEnabled)
                {
                     Unit unitItem = item as Unit;
                     if (unitItem == null || !selected.Contains(unitItem))
                     {
                        item.DisableOutline();
                     }
                }
            }
            dragBuffer.Clear();
            dragging = false; // Важно сбросить флаг
        }
        
        void CancelDrag() // Новый метод для принудительной отмены драга
        {
            if (boxVisual != null)
            {
                boxVisual.gameObject.SetActive(false);
            }
            // Снимаем аутлайны с тех, кто был в буфере
            foreach (var item in dragBuffer)
            {
                if (item != null)
                {
                    Unit unitItem = item as Unit;
                    if (unitItem == null || !selected.Contains(unitItem)) // Не снимать, если уже в selected
                    {
                        item.DisableOutline();
                    }
                }
            }
            dragBuffer.Clear();
            dragging = false;
            Debug.Log("Drag Canceled");
        }


        void ClickSelect()
        {
             // Если режим Attack-Move активен, ЛКМ не должен ничего выделять
            if (attackMoveModeActive) return;

            Vector2 screen = Mouse.current.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(screen);
            Collider2D hit = Physics2D.OverlapPoint(world, selectableMask);

            bool shiftPressed = Keyboard.current.leftShiftKey.isPressed;

            if (hit != null)
            {
                var sel = hit.GetComponent<ISelectable>();
                if (sel != null && sel.Team == teamId)
                {
                    ToggleSelect(sel as Unit, shiftPressed);
                    return; // Выделили/сняли выделение, выходим
                }
            }

            // Если кликнули на пустое место и Shift не нажат, снимаем все выделения
            if (!shiftPressed)
            {
                DeselectAll();
            }
        }

        void BoxSelect()
        {
            // Режим Attack-Move не должен влиять на выделение рамкой
            bool shiftPressed = Keyboard.current.leftShiftKey.isPressed;
            if (!shiftPressed) // Если шифт не нажат, сначала снимаем все текущие выделения
            {
                DeselectAll();
            }

            foreach (var s in dragBuffer) // dragBuffer уже содержит только юнитов нашей команды
            {
                Select(s as Unit); // Select уже добавляет в selected и включает аутлайн
            }
            // dragBuffer будет очищен в EndDrag
        }

        void ToggleSelect(Unit u, bool shiftIsPressed) // Изменен для явной передачи shift
        {
            if (u == null || u.Health == null || !u.Health.IsAlive) return;

            if (shiftIsPressed)
            {
                if (selected.Contains(u))
                {
                    Deselect(u);
                }
                else
                {
                    Select(u);
                }
            }
            else // Shift не нажат, стандартное выделение одного
            {
                DeselectAll();
                Select(u);
            }
        }

        void Select(params Unit[] units)
        {
            foreach (var unit in units)
            {
                if (unit != null && unit.Health != null && unit.Health.IsAlive)
                {
                    if (selected.Add(unit)) // Add возвращает true, если элемент был добавлен (т.е. его не было)
                    {
                        unit.Select(); // Вызываем метод Select самого юнита
                    }
                }
            }
        }

        void Deselect(params Unit[] units)
        {
            foreach (var unit in units)
            {
                if (unit != null)
                {
                    if (selected.Remove(unit)) // Remove возвращает true, если элемент был удален
                    {
                        unit.Deselect(); // Вызываем метод Deselect самого юнита
                    }
                }
            }
        }

        void DeselectAll()
        {
            // Копируем в массив, чтобы избежать ошибки изменения коллекции во время итерации
            Unit[] currentlySelected = selected.ToArray();
            foreach (Unit u in currentlySelected)
            {
                Deselect(u); // Deselect уже удалит из selected и вызовет u.Deselect()
            }
            // selected.Clear(); // После цикла selected должен быть пустым
        }

        // BroadcastAttackMove, BroadcastMove, BroadcastAttack остаются без изменений
        void BroadcastAttackMove(Vector2 targetPoint)
        {
            foreach (Unit u in selected)
            {
                if (u == null || u.AttackComponent == null) continue;
                u.SetCommand(new AttackMoveCommand(targetPoint));
            }
        }

        void BroadcastMove(Vector2 target)
        {
            foreach (Unit u in selected)
            {
                if (u == null) continue;
                u.SetCommand(new MoveCommand(target));
            }
        }

        void BroadcastAttack(IDamageable target)
        {
            foreach (Unit u in selected)
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
                mgr.selected.Remove(u);
            }
            mgr.dragBuffer.Remove(unit);
        }
    }
}
// --- END OF FILE SelectionManager.cs ---