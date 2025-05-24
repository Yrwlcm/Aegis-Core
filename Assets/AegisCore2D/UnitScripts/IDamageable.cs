using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public interface IDamageable
    {
        GameObject MyGameObject { get; } // Свойство для доступа к GameObject'у объекта
        Transform MyTransform { get; } // Свойство для доступа к Transform'у объекта
        int TeamId { get; }         // Команда, чтобы свои не атаковали своих
        bool IsAlive { get; }       // Жив ли объект

        /// <summary>
        /// Наносит урон объекту.
        /// </summary>
        /// <param name="amount">Количество урона.</param>
        /// <param name="attacker">GameObject того, кто нанес урон (может быть null).</param>
        void TakeDamage(float amount, GameObject attacker);
    }
}