// Unit.cs

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

        [SerializeField] private UnitMove moveComponent;
        [SerializeField] private AttackComponent attackComponent;
        [SerializeField] private Outline outline;

        [Header("UI")] [SerializeField] private GameObject healthBarPrefab;
        private HealthBarUI healthBarInstance;
        private static Canvas worldSpaceCanvas;
        private PathDisplay pathDisplay;

        public UnitMove MoveComponent => moveComponent;
        public AttackComponent AttackComponent => attackComponent;
        public HealthComponent Health { get; private set; }


        private IUnitCommand currentCommand;

        private void Awake()
        {
            GameObject = gameObject;
            Health = GetComponent<HealthComponent>();
            moveComponent = GetComponent<UnitMove>(); // Ensure these are assigned
            attackComponent = GetComponent<AttackComponent>(); // Ensure these are assigned
            pathDisplay = GetComponent<PathDisplay>();

            if (Health != null)
            {
                Health.SetTeamId(_teamIdInternal);
                Health.OnDeath += HandleDeath;
            }

            // ... (rest of Awake for HP bar canvas)
             if (worldSpaceCanvas == null)
            {
                GameObject canvasObj = GameObject.FindWithTag("HPBarWorldCanvas");
                if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                if (worldSpaceCanvas == null)
                {
                    canvasObj = GameObject.Find("WorldSpaceUICanvas");
                    if (canvasObj != null) worldSpaceCanvas = canvasObj.GetComponent<Canvas>();
                }

                if (worldSpaceCanvas == null)
                {
                    Debug.LogError("WorldSpaceUICanvas не найден в сцене!");
                }
            }

            if (healthBarPrefab != null && worldSpaceCanvas != null)
            {
                GameObject hbInstanceGo = Instantiate(healthBarPrefab, worldSpaceCanvas.transform);
                healthBarInstance = hbInstanceGo.GetComponent<HealthBarUI>();

                if (healthBarInstance != null)
                {
                    healthBarInstance.SetHealthComponent(this.Health);
                }
                else
                {
                    Debug.LogError("Префаб HP бара не содержит HealthBarUI компонент!", this);
                }
            }
            else
            {
                if (healthBarPrefab == null)
                    Debug.LogWarning("Health Bar Prefab не назначен для юнита: " + gameObject.name, this);
            }
        }

        void LateUpdate()
        {
            if (healthBarInstance != null && healthBarInstance.gameObject.activeSelf && Health.IsAlive)
            {
                healthBarInstance.transform.position = transform.position;
            }
            else if (healthBarInstance != null && !Health.IsAlive && healthBarInstance.gameObject.activeSelf)
            {
                // HealthBarUI now handles its own destruction on target death
                // healthBarInstance.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            SelectionManager.RegisterUnitForTeam(this, Team);
        }

        private void HandleDeath(GameObject attacker)
        {
            Debug.Log($"Unit {gameObject.name} is handling its death. Attacker: {attacker?.name}");
            ClearCurrentCommand();
            // SelectionManager.RemoveUnitForTeam(this, Team); // Moved to OnDestroy for robustness
            if (Selected) Deselect();
        }

        private void Update()
        {
            if (!Health.IsAlive)
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
                        // If unit is now stationary attacking, path display will still show line to target
                    }
                }
                else
                {
                    currentCommand.Execute(this);
                    if (currentCommand is MoveCommand && MoveComponent.HasReachedDestination())
                    {
                        ClearCurrentCommand();
                    }
                }
            }
        }
        
        private void UpdatePathDisplayForCurrentCommand()
        {
            if (pathDisplay == null || !pathDisplay.isActiveAndEnabled) return;

            // This method sets the *state* for PathDisplay.
            // PathDisplay.Update() will use this state if 'visible' is true.
            if (currentCommand is AttackCommand attackCmd && attackCmd.IsTargetStillValid())
            {
                pathDisplay.SetPathMode(true); // Sets color to red
                pathDisplay.SetAttackTargetOverride(attackCmd.GetTarget());
            }
            else if (currentCommand is MoveCommand)
            {
                pathDisplay.SetPathMode(false); // Sets color to default
                pathDisplay.SetAttackTargetOverride(null);
            }
            else // No command or an unknown command type
            {
                pathDisplay.SetPathMode(false); // Default color
                pathDisplay.SetAttackTargetOverride(null); // No specific attack target
            }
        }

        public void SetCommand(IUnitCommand cmd)
        {
            if (!Health.IsAlive) return;

            if (currentCommand is MoveCommand oldMoveCmd && cmd is MoveCommand newMoveCmd)
            {
                if (Vector3.Distance(MoveComponent.GetDestination(), newMoveCmd.GetTargetPosition_DEBUG()) < 0.1f)
                {
                    return;
                }
            }

            if (currentCommand is AttackCommand oldAttackCmd && cmd is AttackCommand newAttackCmd)
            {
                if (oldAttackCmd.GetTarget() == newAttackCmd.GetTarget())
                {
                    return;
                }
            }
            
            currentCommand = cmd;
            UpdatePathDisplayForCurrentCommand(); // Update path display based on new command
        }

        public void ClearCurrentCommand()
        {
            if (currentCommand != null && currentCommand is AttackCommand)
            {
                if (MoveComponent.IsMoving())
                {
                    MoveComponent.Stop();
                }
            }
            currentCommand = null;
            UpdatePathDisplayForCurrentCommand(); // Update path display as command is cleared
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
            if (Health.IsAlive) // Only show outline if alive
            {
                outline.Show(true);
                OutlineEnabled = true;
            }
        }

        public void DisableOutline()
        {
            outline.Show(false); // Always allow disabling outline
            OutlineEnabled = false;
        }

        public void Select()
        {
            if (!Health.IsAlive) return;
            EnableOutline();
            Selected = true;

            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(true); // Make it visible
                UpdatePathDisplayForCurrentCommand(); // Then update its content based on current command
            }
        }

        public void Deselect()
        {
            DisableOutline(); // This will also hide outline if unit died while selected
            Selected = false;
            if (pathDisplay != null)
            {
                pathDisplay.SetVisible(false);
            }
        }
    }
}