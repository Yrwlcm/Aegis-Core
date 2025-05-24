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

            foreach (var sel in pool) // sel это ISelectable
            {
                Vector2 scr = cam.WorldToScreenPoint(sel.GameObject.transform.position);
                if (dragRect.Contains(scr))
                {
                    // Подсвечиваем только своих юнитов во время драггинга
                    if (sel.Team == teamId)
                    {
                        if (dragBuffer.Add(sel)) sel.EnableOutline();
                    }
                }
                else // Если юнит не в прямоугольнике выделения
                {
                    if (dragBuffer.Remove(sel)) // Если он был в буфере
                    {
                        // Снимаем аутлайн только если он не в основном списке выбранных `selected`
                        // Иначе, если он уже был выбран до драггинга, аутлайн снимется зря.
                        // Unit должен быть приводимым к Unit, чтобы проверить selected.Contains
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
            foreach (var s in dragBuffer)
            {
                if (s.Team != teamId) // this.teamId - это teamId текущего SelectionManager'а
                    continue;

                Select((Unit)s);
            }
        }

        void HandleCommandInput()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (selected.Count == 0) return;

                Vector2 screen = Mouse.current.position.ReadValue();
                Vector2 world = cam.ScreenToWorldPoint(screen);

                // Проверяем, есть ли под курсором объект для атаки
                // Используем Physics2D.OverlapPoint для поиска коллайдера
                Collider2D
                    hitAttackable =
                        Physics2D.OverlapPoint(world, attackableMask); // attackableMask должна быть настроена на врагов

                if (hitAttackable != null)
                {
                    IDamageable damageableTarget = hitAttackable.GetComponent<IDamageable>();
                    // Также можно проверить GetComponentInParent или GetComponentInChildren, если IDamageable не на том же объекте, что и коллайдер.

                    if (damageableTarget != null && damageableTarget.IsAlive)
                    {
                        // Проверяем, что цель не из нашей команды (если это не дружественный огонь :))
                        // Это дублирует проверку в AttackComponent, но здесь это для UI/UX - чтобы курсор менялся и т.д.
                        bool isOwnTeamTarget = false;
                        if (selected.Count > 0) // Берем команду первого выделенного юнита для сравнения
                        {
                            Unit firstSelectedUnit = selected.First(); // LINQ, нужно using System.Linq;
                            if (firstSelectedUnit.Team == damageableTarget.TeamId &&
                                damageableTarget.TeamId != -1) // -1 может быть нейтральной командой
                            {
                                isOwnTeamTarget = true;
                            }
                        }

                        if (!isOwnTeamTarget)
                        {
                            Debug.Log($"Commanding selected units to attack {damageableTarget.MyGameObject.name}");
                            BroadcastAttack(damageableTarget);
                            return; // Команда атаки выдана, выходим
                        }
                        // Если цель своя, то можно либо ничего не делать, либо выдать команду Move (например, следовать за союзником)
                        // Пока просто проигнорируем и перейдем к MoveCommand ниже, если это был союзник
                    }
                }

                // Если не кликнули по врагу (или кликнули по союзнику/пустому месту), даем команду Move
                //Debug.Log($"Commanding selected units to move to {world}");
                BroadcastMove(world, (targetCenter, unit, index) => targetCenter);
            }
        }

        void BroadcastMove(Vector2 target, Func<Vector2, Unit, int, Vector2> modifyTarget)
        {
            var index = 0;
            foreach (Unit u in selected)
                u.SetCommand(new MoveCommand(modifyTarget(target, u, index++)));
        }

        void BroadcastAttack(IDamageable target)
        {
            foreach (Unit u in selected)
                u.SetCommand(new AttackCommand(u, target)); // Передаем самого юнита и цель
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
            }

            selected.UnionWith(units);
        }

        void Deselect(params Unit[] units)
        {
            foreach (var unit in units)
            {
                unit.Deselect();
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