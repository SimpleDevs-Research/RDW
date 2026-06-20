using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class PedestrianOccluder : MonoBehaviour
    {
        void OnTriggerEnter(Collider other) {
            Pedestrian p = other.GetComponent<Pedestrian>();
            if (p != null) p.ToggleAnimation(true);
        }

        void OnTriggerExit(Collider other) {
            Pedestrian p = other.GetComponent<Pedestrian>();
            if (p != null) p.ToggleAnimation(false);
        }
    }
}
