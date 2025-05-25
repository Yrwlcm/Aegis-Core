// ExplosionEffectHandler.cs
using UnityEngine;
public class ExplosionEffectHandler : MonoBehaviour
{
    public void DisableEffect()
    {
        // Можно просто выключить SpriteRenderer, если объект используется повторно (пулинг)
        // SpriteRenderer sr = GetComponent<SpriteRenderer>();
        // if (sr != null) sr.enabled = false;

        // Или уничтожить весь объект эффекта, если он создается каждый раз
        Destroy(gameObject); 
    }

    // Альтернатива: сделать его неактивным, чтобы его можно было переиспользовать из пула
    public void DeactivateEffect()
    {
        gameObject.SetActive(false);
    }
}