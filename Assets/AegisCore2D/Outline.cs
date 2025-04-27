using UnityEngine;

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class Outline : MonoBehaviour
{
    [SerializeField] private SpriteRenderer outlineRenderer;

    public void Show(bool state) => outlineRenderer.enabled = state;
}
