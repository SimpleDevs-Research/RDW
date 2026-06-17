using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim
{
    [System.Serializable]
    public class RandomFloat
    {
        public float value;
        public bool randomize = false;
        public Vector2 limits = Vector2.zero;
        public RandomFloat(float value)
        {
            this.value = value;
            this.randomize = false;
            this.limits = Vector2.zero;
        }
        public RandomFloat(float value, bool randomize)
        {
            this.value = value;
            this.randomize = randomize;
            this.limits = Vector2.zero;
        }
        public RandomFloat(float value, bool randomize, Vector2 limits)
        {
            this.value = value;
            this.randomize = randomize;
            this.limits = limits;
        }
        public void Randomize()
        {
            if (!randomize) return;
            this.value = UnityEngine.Random.Range(limits.x, limits.y);
        }
        public static implicit operator float(RandomFloat myFloat)
        {
            return myFloat.value;
        }
        public static implicit operator RandomFloat(float value)
        {
            return new RandomFloat(value);
        }

        // Override ToString for easy debugging
        public override string ToString()
        {
            return this.value.ToString();
        }
    }
}