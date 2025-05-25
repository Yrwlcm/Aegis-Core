using System.Collections.Generic;
using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public static class Teams
    {
        // Consider making this a ReadOnlyDictionary after initialization if colors are static.
        // For now, public static Dictionary is fine.
        public static readonly Dictionary<int, Color> Colors = new()
        {
            { 0, Color.green },
            { 1, Color.red },
            { 2, Color.blue },
            { 3, Color.cyan },
            { 4, Color.yellow },
            { 5, Color.magenta },
            // Add more teams and colors as needed
        };

        // Helper method to get color, defaulting if team ID not found
        public static Color GetTeamColor(int teamId, Color defaultColor = default)
        {
            return Colors.TryGetValue(teamId, out var color) ? color : defaultColor;
        }
    }
}