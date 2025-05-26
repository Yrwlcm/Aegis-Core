using AegisCore2D.GeneralScripts;
using TMPro;
using UnityEngine;

namespace AegisCore2D.Buildings
{
    public class EnergySource : Building
    {
        public SpriteRenderer spriteRenderer;
        public EnergyManager energyManager;

        [Header("Animation")] [SerializeField] private Animator animator; // обычный Animator на объекте
        [SerializeField] private string capturedTrigger = "Captured";

        private static Canvas worldCanvas; // общий World-space canvas
        private bool captured;

        protected override void HandleDeath(GameObject attacker)
        {
            if (captured) return; // защита от повторного вызова
            captured = true;

            // 1.- находим world-canvas (тот же, что HPBars ставит Building)
            if (worldCanvas == null)
            {
                var go = GameObject.FindWithTag("HPBarWorldCanvas") ??
                         GameObject.Find("WorldSpaceUICanvas");
                worldCanvas = go != null ? go.GetComponent<Canvas>() : null;
                if (worldCanvas == null)
                    Debug.LogError("WorldSpaceUICanvas not found for EnergySource!");
            }


            // 3.- визуально «перекрашиваем» или что-нибудь ещё
            spriteRenderer.color = Color.white;

            // 4.- включаем анимацию захвата
            if (animator != null) animator.SetTrigger(capturedTrigger);

            // 5.- даём менеджеру знать
            energyManager.IncreaseCapturedSourcesCount();
        }
    }
}