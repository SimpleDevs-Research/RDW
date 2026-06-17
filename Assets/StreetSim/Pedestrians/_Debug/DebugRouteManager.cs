using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class DebugRouteManager : RouteNode
    {
        public RouteNode destination;
        public Pedestrian.Personality personality;

        private Vector3 prevPosition;
        private Vector3 prevDestinationPosition;
        public List<RouteNode> trajectory = new();

        private void Awake() {
            prevPosition = transform.position;
            prevDestinationPosition = destination.position;
        }

        private void Start() {
            trajectory = RouteManager.Instance.GetRouteFromCustomNodes(this, destination, personality);
        }

        private void Update() {
            if (prevPosition != transform.position || prevDestinationPosition != destination.position) {
                trajectory = RouteManager.Instance.GetRouteFromCustomNodes(this, destination, personality);
                prevPosition = transform.position;
                prevDestinationPosition = destination.position;
            }
        }

        private void OnDrawGizmosSelected() {
            if (!Application.isPlaying || destination == null) return;
            if (trajectory.Count == 0) return;
            Gizmos.color = Color.white;
            for(int i = 0; i < trajectory.Count-1; i++) {
                Gizmos.DrawLine(trajectory[i].position, trajectory[i+1].position);
            }
        }
    }
}
