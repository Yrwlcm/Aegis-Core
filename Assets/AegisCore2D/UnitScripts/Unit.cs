// --- START OF FILE Unit.cs ---
using System.Collections.Generic;
using AegisCore2D.GeneralScripts;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(HealthComponent), typeof(UnitMove), typeof(AttackComponent))]
    public class Unit : MonoBehaviour, ISelectable
    {
        public GameObject GameObject { get; private set; }
        public bool Selected { get; private set; }
        public bool OutlineEnabled { get; private set; }

        [SerializeField] private int _teamIdInternal;

        public int Team
        {
            get => Health?.TeamId ?? _teamIdInternal;
            set
            {
                _teamIdInternal = value;
                if (Health != null)
                {
                    Health.SetTeamId(value);
                }
            }
        }

        [Header("Attack-Move Settings")]
        [SerializeField] private float attackMoveScanRadiusMultiplier = 1.5f; // Множитель для радиуса сканирования
        [SerializeField] private float minAttackMoveScanRadius = 3f;      // Минимальный радиус сканирования

        [Header("Component References")]
        [SerializeField] private UnitMove moveComponent;
        [SerializeField] private AttackComponent attackComponent;
        [SerializeField] private Outline outline;

        [Header("UI")]
        [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas;
        private PathDisplay pathDisplay;

        public UnitMove MoveComponent => moveComponent;
        public AttackComponent AttackComponent => attackComponent;
        public HealthComponent Health { get; private set; }
        public float AttackMoveScanRadiusMultiplier => attackMoveScanRadiusMultiplier; // Геттер
        public float MinAttackMoveScanRadius => minAttackMoveScanRadius; // Геттер


        private IUnitCommand currentCommand;

        private void Awake()
        {
            GameObject = gameObject;
            Health = GetComponent<HealthComponent>();
            moveComponent = GetComponent<UnitMove>();
            attackComponent = GetComponent<AttackComponent>();
            pathDisplay = GetComponent<PathDisplay>();
            // Предполагаем, что Outline - это дочерний объект с компонентом Outline
            // Если он на том же объекте, то GetComponent<Outline>()
            outline = GetComponentInChildren<Outline>();


            if (Health != null)
            {
                Health.SetTeamId(_teamIdInternal);
                Health.OnDeath += HandleDeath;
            }
            else
            {
                Debug.LogError("Unit is missing HealthComponent!", this);
            }

            if (moveComponent == null) Debug.LogError("Unit is missing UnitMove component!", this);
            if (attackComponent == null) Debug.LogError("Unit is missing AttackComponent!", this);
            if (pathDisplay == null) Debug.LogWarning("Unit is missing PathDisplay component (optional).", this);
            if (outline == null) Debug.LogWarning("Unit is missing Outline component or it's not a child (optional).", this);


            if (worldSpaceCanvas == null)
            {
                GameObject canvasObj = GameObject.FindWithTag("HPBarWorldCanvas");
                if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                else
                {
                    canvasObj = GameObject.Find("WorldSpaceUICanvas");
                    if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                }

                if (worldSpaceCanvas == null)
                {
                     Debug.LogError("Critical: WorldSpaceUICanvas with tag 'HPBarWorldCanvas' or name 'WorldSpaceUICanvas' not found in scene!");
                }
            }

            if (healthBarPrefab != null && worldSpaceCanvas != null)
            {
                GameObject hbInstanceGo = Instantiate(healthBarPrefab, worldSpaceCanvas.transform);
                healthBarInstance = hbInstanceGo.GetComponent<HealthBarUI>();

                if (healthBarInstance != null)
                {
                    if (Health != null) healthBarInstance.SetHealthComponent(this.Health);
                     else Debug.LogError("Cannot set HealthComponent for HealthBarUI because Unit's Health is null.", this);
                }
                else
                {
                    Debug.LogError("Health Bar Prefab does not contain HealthBarUI component!", this);
                }
            }
            else
            {
                if (healthBarPrefab == null) Debug.LogWarning("Health Bar Prefab not assigned for unit: " + gameObject.name, this);
            }
        }

        void LateUpdate()
        {
            if (healthBarInstance != null && Health != null && healthBarInstance.gameObject.activeSelf && Health.IsAlive)
            {
                healthBarInstance.transform.position = transform.position;
            }
        }

        private void Start()
        {
            SelectionManager.RegisterUnitForTeam(this, Team);
        }

        private void HandleDeath(GameObject attacker)
        {
            ClearCurrentCommand();
            if (Selected) Deselect();
        }

        private void Update()
        {
            if (Health == null || !Health.IsAlive)
            {
                if (currentCommand != null) ClearCurrentCommand();
                return;
            }

            if (currentCommand != null)
            {
                if (currentCommand is AttackCommand attackCmd)
                {
                    if (!attackCmd.IsTargetStillValid())
                    {
                        ClearCurrentCommand();
                    }
                    else
                    {
                        currentCommand.Execute(this);
                    }
                }
                else if (currentCommand is AttackMoveCommand) // AttackMoveCommand сам обрабатывает переход в AttackCommand
                {
                    currentCommand.Execute(this);
                }
                else if (currentCommand is MoveCommand)
                {
                    currentCommand.Execute(this);
                    if (MoveComponent != null && MoveComponent.HasReachedDestination())
                    {
                        ClearCurrentCommand();
                    }
                }
                else
                {
                    currentCommand.Execute(this); // Для других возможных команд
                }
            }
        }
        
        private void UpdatePathDisplayForCurrentCommand()
        {
            if (pathDisplay == null || !pathDisplay.isActiveAndEnabled) return;

            if (!Selected)
            {
                pathDisplay.SetVisible(false);
                return;
            }
            pathDisplay.SetVisible(true);

            if (currentCommand is AttackCommand attackCmd && attackCmd.IsTargetStillValid())
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.Attack);
                pathDisplay.SetAttackTargetOverride(attackCmd.GetTarget());
            }
            else if (currentCommand is AttackMoveCommand)
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.AttackMove);
                pathDisplay.SetAttackTargetOverride(null);
            }
            else if (currentCommand is MoveCommand)
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.Default);
                pathDisplay.SetAttackTargetOverride(null);
            }
            else
            {
                pathDisplay.SetDisplayMode(PathDisplay.PathDisplayMode.None);
                pathDisplay.SetAttackTargetOverride(null);
            }
        }

        public void SetCommand(IUnitCommand cmd)
        {
            if (Health == null || !Health.IsAlive) return;

            // Оптимизация: не переназначать идентичную команду
            if (currentCommand is MoveCommand oldMoveCmd && cmd is MoveCommand newMoveCmd)
            {
                if (MoveComponent != null && Vector3.Distance(MoveComponent.GetDestination(), newMoveCmd.GetTargetPosition_DEBUG()) < 0.1f) return;
            }
            if (currentCommand is AttackCommand oldAttackCmd && cmd is AttackCommand newAttackCmd)
            {
                if (oldAttackCmd.GetTarget() == newAttackCmd.GetTarget()) return;
            }
            if (currentCommand is AttackMoveCommand oldAttackMoveCmd && cmd is AttackMoveCommand newAttackMoveCmd)
            {
                 if (Vector3.Distance(oldAttackMoveCmd.GetTargetPosition_DEBUG(), newAttackMoveCmd.GetTargetPosition_DEBUG()) < 0.1f) return;
            }

            currentCommand = cmd;
            UpdatePathDisplayForCurrentCommand();
        }

        public void ClearCurrentCommand()
        {
            if (currentCommand != null && currentCommand is AttackCommand)
            {
                if (MoveComponent != null && MoveComponent.IsMoving())
                {
                    MoveComponent.Stop(); // Если отменяем атаку, и юнит двигался к ней, останавливаем.
                }
            }
            currentCommand = null;
            UpdatePathDisplayForCurrentCommand();
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.OnDeath -= HandleDeath;
            }
            SelectionManager.RemoveUnitForTeam(this, Team);
        }

        public void EnableOutline()
        {
            if (outline != null && Health != null && Health.IsAlive)
            {
                outline.Show(true);
                OutlineEnabled = true;
            }
        }

        public void DisableOutline()
        {
            if (outline != null)
            {
                outline.Show(false);
                OutlineEnabled = false;
            }
        }

        public void Select()
        {
            if (Health == null || !Health.IsAlive) return;
            EnableOutline();
            Selected = true;
            UpdatePathDisplayForCurrentCommand();
        }

        public void Deselect()
        {
            DisableOutline();
            Selected = false;
            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(false);
            }
        }
        
        public IUnitCommand CurrentCommand_DEBUG()
        {
            return currentCommand;
        }
    }
}
// --- END OF FILE Unit.cs ---