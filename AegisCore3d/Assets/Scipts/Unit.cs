using Scipts.Interfaces;
using UnityEngine;

namespace Scipts
{
    public class Unit : MonoBehaviour, ISelectable
    {
        public GameObject GameObject { get; private set; }
        public bool Selected { get; private set; }
        public bool OutlineEnabled { get; private set; }

        private Outline outline;
        private UnitMovement movement;

        private void Start()
        {
            GameObject = gameObject;
            SelectionManager.Instance.allUnits.Add(this);
            outline = GetComponent<Outline>();
            movement = GetComponent<UnitMovement>();
        }

        private void OnDestroy()
        {
            SelectionManager.Instance.allUnits.Remove(this);
        }

        public void EnableOutline()
        {
            OutlineEnabled = true;
            outline.enabled = true;
        }

        public void DisableOutline()
        {
            OutlineEnabled = false;
            outline.enabled = false;
        }

        public void Select()
        {
            EnableOutline();
            Selected = true;
            movement.enabled = true;
        }

        public void Deselect()
        {
            DisableOutline();
            Selected = false;
            movement.enabled = false;
        }
    }
}