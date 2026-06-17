using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public static class Extensions {

        public static Vector2 ToVector2(this Vector3 v) {
            return new Vector2(v.x, v.z);
        }

        // Old code, but kept here for posterity
        public static int GetMinIndex(this float[] a, out float value) {
            int index = 0;
            value = a[0];
            for(int i = 0; i < a.Length; i++) {
                float av = a[i];
                if (av < value) {
                    index = i;
                    value = av;
                }
            }
            return index;
        }

    }
}
