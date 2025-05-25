using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AegisCore2D.GeneralScripts
{
    public class EnergyManager : MonoBehaviour
    {
        public TextMeshProUGUI energyText; // Ссылка на текстовый элемент UI
        private int energy = 0;
        private float timer = 0f;
        private const float updateInterval = 1f; // Интервал обновления в секундах

        public void DecreaseEnergy(int amount)
        {
            energy -= amount;
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
                energy += 5;
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