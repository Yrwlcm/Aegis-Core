using System.Collections.Generic;
using Scipts.Interfaces;
using UnityEngine;

namespace AegisCore2D
{
    public class Unit : MonoBehaviour, ISelectable
    {
        public GameObject GameObject { get; private set; }
        public bool Selected { get; private set; }
        public bool OutlineEnabled { get; private set; }
        public int Team { get; set; }
        [SerializeField] private UnitMove moveComponent;
        [SerializeField] private Outline outline;
        public UnitMove MoveComponent => moveComponent;
        private readonly Queue<IUnitCommand> queue = new();
        

        private void Start()
        {
            GameObject = gameObject;
            SelectionManager.RegisterUnitForTeam(this, Team);
        }

        private void Update()
        {
            if (queue.Count == 0) return;
            queue.Dequeue().Execute(this);
        }
        
        public void Enqueue(IUnitCommand cmd) => queue.Enqueue(cmd);

        private void OnDestroy()
        {
            Deselect();
            SelectionManager.RemoveUnitForTeam(this, Team);
        }

        public void EnableOutline()  { outline.Show(true);  OutlineEnabled = true; }
        public void DisableOutline() { outline.Show(false); OutlineEnabled = false; }

        public void Select()
        {
            EnableOutline();
            Selected = true;
        }

        public void Deselect()
        {
            DisableOutline();
            Selected = false;
        }
    }
}