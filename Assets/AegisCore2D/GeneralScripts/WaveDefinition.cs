using UnityEngine;

namespace AegisCore2D.GameManagement
{
    [System.Serializable] // Чтобы отображался в инспекторе как часть списка
    public class WaveDefinition
    {
        [Tooltip("Префаб врага для этой волны.")]
        public GameObject enemyPrefab;
        [Tooltip("Количество врагов в этой волне.")]
        public int enemyCount = 5;
        [Tooltip("Точка спауна для этой волны. Если не указана, будет использована точка по умолчанию из WaveSpawner.")]
        public Transform spawnPointOverride; // Можно использовать Vector3, если точки не являются объектами сцены
        [Tooltip("Задержка перед началом этой волны (в секундах) после окончания предыдущей или начала игры.")]
        public float delayBeforeWave = 10f;
        [Tooltip("Интервал между спауном каждого врага в этой волне (в секундах).")]
        public float spawnInterval = 1f;
        [Tooltip("Сообщение, отображаемое перед началом этой волны.")]
        public string waveAnnouncement = "Следующая волна скоро начнется!";

        // Можно добавить еще параметры:
        // public float healthMultiplier = 1f;
        // public float damageMultiplier = 1f;
        // public float speedMultiplier = 1f;
    }
}