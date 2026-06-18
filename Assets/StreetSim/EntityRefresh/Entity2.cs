using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {

    [System.Flags]
    public enum EntityType { 
        Pedestrian=1, 
        Vehicle=2, 
        TrafficLight=4, 
        PedestrianLight=8, 
        Crosswalk=16, 
        Obstacle=32, 
        Hand=64 
    }

    public abstract class Entity2 : MonoBehaviour
    {
        // =================================
        [Header("=== Entity Settings ===")]
        // =================================
        [SerializeField] protected EntityType type;
        [SerializeField] protected Color color = Color.clear;
    }

}