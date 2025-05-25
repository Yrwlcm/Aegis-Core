using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AegisCore2D.GeneralScripts
{
    public class Barrack : MonoBehaviour
    {
        public float spawnOffset = 1f;

        public void SpawnPrefab(GameObject prefabToSpawn)
        {
            if (prefabToSpawn != null)
            {
                // Позиция спавна - перед этим объектом
                Vector3 spawnPosition = transform.position + transform.forward * spawnOffset;
            
                // Создаем экземпляр префаба
                Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("Prefab to spawn is not assigned!");
            }
        }
    }
}