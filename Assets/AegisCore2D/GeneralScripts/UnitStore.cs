using UnityEngine;

namespace AegisCore2D.GeneralScripts
{
    public class UnitStore : MonoBehaviour
    {
        public EnergyManager energyManager;
        public Barrack barrack;
        public int unitPrice;

        public void BuyUnit(GameObject prefabToSpawn)
        {
            if (energyManager != null && energyManager.GetCurrentEnergy() >= unitPrice)
            {
                energyManager.DecreaseEnergy(unitPrice);
                barrack.SpawnPrefab(prefabToSpawn);
            }
        }
    }
}