using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace StreetSim {
    public class Entity : MonoBehaviour
    {
        [System.Flags]
        public enum Type { 
            Pedestrian=1, 
            Vehicle=2, 
            TrafficLight=4, 
            PedestrianLight=8, 
            Crosswalk=16, 
            Obstacle=32, 
            Hand=64 
        }

        // =================================
        [Header("=== Entity Settings ===")]
        // =================================
        public Type type;
        [SerializeField] protected Color m_color = Color.clear;
        [SerializeField] protected float m_avoidanceRadius = 0f;

        // =================================
        [Header("=== Cached Data - Read-Only ===")]
        // =================================
        public Vector3 velocity;
        public Vector3 displacement;

        // =================================
        [Header("=== Events ===")]
        // =================================
        public UnityEvent<Entity> onEnabled;
        public UnityEvent<Entity> onDisabled;

        // =================================
        // === GETTERS ===
        // =================================
        public Vector3 position => transform.position;
        public float avoidanceRadius => m_avoidanceRadius;
        public Color color { get => m_color; set {} }
        public float speed => velocity.magnitude;

        // =================================
        // === PRIVATE ONLY - no peeking ===
        // =================================
        private Vector3 _prevPosition;
        private Type _prevType;

        protected virtual void OnValidate() {
            if (_prevType == type) return;
            switch(type) {
                case Type.Pedestrian:
                    m_color = Color.red;
                    break;
                case Type.Vehicle:
                    m_color = Color.blue;
                    break;
                case Type.TrafficLight:
                    m_color = Color.yellow;
                    break;
                case Type.PedestrianLight:
                    m_color = Color.green;
                    break;
                case Type.Crosswalk:
                    m_color = Color.white;
                    break;
                default:
                    m_color = Color.clear;
                    break;
            }
            _prevType = type;
        }

        protected virtual void Awake() {}

        protected virtual void Start() {
            displacement = Vector3.zero;
            velocity = Vector3.zero;
            _prevPosition = transform.position;        
        }

        protected virtual void FixedUpdate() {
            displacement = transform.position - _prevPosition;
            velocity = displacement / Time.fixedDeltaTime;
            _prevPosition = transform.position;
        }

        private void OnEnable() {
            onEnabled?.Invoke(this);
        }
        private void OnDisable() {
            onDisabled?.Invoke(this);
        }

    }

}
