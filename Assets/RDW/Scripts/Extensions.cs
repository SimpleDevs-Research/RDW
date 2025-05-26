using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW
{
    public static class Extensions
    {
        public static Vector3 Flatten(this Vector3 v, float y = 0f)
        {
            return new Vector3(v.x, y, v.z);
        }
    }   
}
