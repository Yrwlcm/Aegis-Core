using UnityEngine;

namespace Scipts.Interfaces
{
    public interface ISelectable
    {
        public GameObject GameObject { get; }
        public bool Selected { get; }
        public bool OutlineEnabled { get; }
        public int Team { get; set; }

        public void EnableOutline();
        public void DisableOutline();

        public void Select();
        public void Deselect();
    }
}