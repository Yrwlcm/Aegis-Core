using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public interface IDamageable
    {
        GameObject MyGameObject { get; }
        Transform MyTransform { get; }
        int TeamId { get; }
        bool IsAlive { get; }

        void TakeDamage(float amount, GameObject attacker);
    }
}