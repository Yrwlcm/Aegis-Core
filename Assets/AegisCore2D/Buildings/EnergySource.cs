using AegisCore2D.GeneralScripts;
using TMPro;
using UnityEngine;

namespace AegisCore2D.Buildings
{
    public class EnergySource : Building
    {
        public SpriteRenderer spriteRenderer;
        public EnergyManager energyManager;
        public GameObject infoText;

        protected override void HandleDeath(GameObject attacker)
        {
            infoText.SetActive(true);
            spriteRenderer.color = Color.white;
            energyManager.IncreaseCapturedSourcesCount();
        }
    }
}