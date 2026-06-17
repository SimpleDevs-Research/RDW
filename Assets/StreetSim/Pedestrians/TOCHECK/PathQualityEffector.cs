using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class PathQualityEffector : MonoBehaviour
    {
        public enum effectType { 
            cleanliness,
            safety
        }
        public effectType myEffect;
        public float effectLevel;
        public float timeToDisappear = 40;
        public bool disappear = true;
        public bool rotate = true;

        private void Start() {
            if(rotate) transform.Rotate(new Vector3(0, Random.Range(0, 360), 0));
            RaycastHit hit;
            if ((Physics.Raycast(transform.position, Vector3.down, out hit, 10f))) {
                if (hit.distance > 0.3f) {
                    transform.position += Vector3.down * hit.distance;
                }
            }
        }

        private void Update() {
            timeToDisappear -= Time.deltaTime;
            if(timeToDisappear <= 0 && disappear) {
                Destroy(gameObject);
            }
        }
    }
}