using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public interface ISelectable
    {
        GameObject GameObject { get; }
        bool Selected { get; } // Should reflect if the unit is part of the current primary selection
        bool OutlineEnabled { get; } // Should reflect visual state of any outline (temp or permanent)
        int Team { get; set; } // Team of the selectable unit

        void EnableOutline();
        void DisableOutline();

        void Select();   // Mark as primarily selected
        void Deselect(); // Remove from primary selection
    }
}