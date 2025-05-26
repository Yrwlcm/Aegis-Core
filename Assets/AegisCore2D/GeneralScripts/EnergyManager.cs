using TMPro;
using UnityEngine;

namespace AegisCore2D.GeneralScripts
{
    public class EnergyManager : MonoBehaviour
    {
        public TextMeshProUGUI energyText; // Ссылка на текстовый элемент UI
        private int energy;
        private float timer;
        private const float updateInterval = 1f;
        private int capturedSourcesCount;

        public void DecreaseEnergy(int amount)
        {
            energy -= amount;
        }

        public void IncreaseCapturedSourcesCount()
        {
            capturedSourcesCount++;
        }

        public int GetCurrentEnergy() => energy;

        void Start()
        {
            UpdateEnergyDisplay();
        }

        void Update()
        {
            timer += Time.deltaTime;
        
            if (timer >= updateInterval)
            {
                timer = 0f;
                energy += 5 + 3 * capturedSourcesCount;
                UpdateEnergyDisplay();
            }
        }

        void UpdateEnergyDisplay()
        {
            if (energyText != null)
            {
                energyText.text = "Энергия: " + energy.ToString();
            }
        }
    }
}