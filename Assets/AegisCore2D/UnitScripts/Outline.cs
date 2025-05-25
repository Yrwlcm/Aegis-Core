using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Outline : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer outlineRenderer;

        // Ensure outlineRenderer is assigned, e.g., in Awake or via Inspector
        private void Awake()
        {
            if (outlineRenderer == null)
            {
                // Attempt to get it from children if not directly assigned
                outlineRenderer = GetComponentInChildren<SpriteRenderer>();
                if (outlineRenderer == this.GetComponent<SpriteRenderer>()) // If it's the main SR
                {
                    Debug.LogError("Outline component's outlineRenderer should be a separate SpriteRenderer, typically on a child object for layering.", this);
                    // Create one dynamically or disable? For now, log error.
                    // This setup implies the Outline object IS the outline sprite, or controls one.
                }
            }
            if (outlineRenderer == null) // Still null
            {
                Debug.LogError("OutlineRenderer is not assigned and could not be found on Outline component.", this);
                enabled = false;
            }
        }

        public void Show(bool state)
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.enabled = state;
            }
        }
    }
}