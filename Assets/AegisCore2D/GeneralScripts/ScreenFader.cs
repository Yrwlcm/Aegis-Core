using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AegisCore2D.GeneralScripts
{
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private Image fadeImage;
        [SerializeField] private float defaultDuration = 0.5f;
        [SerializeField] private Color color = Color.black;
    
        private Canvas canvas;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (fadeImage == null)
                fadeImage = GetComponentInChildren<Image>();
        
            canvas = GetComponent<Canvas>();
            fadeImage.color = new Color(color.r, color.g, color.b, 0f);
            fadeImage.raycastTarget = true;
        }

        public IEnumerator FadeOut(float duration = -1f) =>
            Fade(0f, 1f, duration < 0 ? defaultDuration : duration, fadeOut: true);

        public IEnumerator FadeIn(float duration = -1f) =>
            Fade(1f, 0f, duration < 0 ? defaultDuration : duration, fadeOut: false);
    
        private IEnumerator Fade(float from, float to, float duration, bool fadeOut)
        {
            if (fadeOut)
                canvas.enabled = true;

            var t = 0f;
            var c = color;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var a = Mathf.Lerp(from, to, t / duration);
                fadeImage.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            fadeImage.color = new Color(c.r, c.g, c.b, to);
        
            if (!fadeOut)
                canvas.enabled = false;
        }
    }
}