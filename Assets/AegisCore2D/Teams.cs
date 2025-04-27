using System.Collections.Generic;
using UnityEngine;

namespace Scipts
{
    public static class Teams
    {
        public static Dictionary<int, Color> Colors = new()
        {
            { 0, Color.green },
            { 1, Color.red },
            { 2, Color.blue },
            { 3, Color.cyan },
            { 4, Color.yellow },
            { 5, Color.magenta },
        };
    }
}