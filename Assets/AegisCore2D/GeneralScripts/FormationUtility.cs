using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public static class FormationUtility
    {
        public static Vector2 GetSpiralOffset(int index, float spacing)
        {
            var angle = index * 137.508f * Mathf.Deg2Rad;
            var radius = spacing * Mathf.Sqrt(index);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}