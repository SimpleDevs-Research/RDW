using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.AI;
using Unity.Jobs;
using Unity.Burst;
using DataStructures.ViliWonka.KDTree;
using RVO;
//using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StreetSim {
    //This script directs pedestrian behaviors. As far as an individual pedestrian unit goes, this is the highest level for the movement/navigation system.
    public class Pedestrian : RVOEntity
    {
        // ============================================
        // === SERIALIZABLES ===
        // ============================================ 
        public enum Intention { 
            IDLE,       // Idling. Not moving to another position or anything.
            TRAVEL,     // Traveling from a start position to an end.
            APPROACH,   // Approaching ad following a specific target of attention
            WATCH       // Basically a non-value.
        }

        [System.Serializable]
        public class Personality {
            [Header("=== Dijkstra Elements ===")]
            public float riskAversion;
            public float dirtinessAversion;
            public float crowdednessAversion;
            public float distanceAversion;
            public float litterInclination;
            [Header("=== Static Preferences ===")]
            [Min(0.1f), Tooltip("How far away do we have to deviate from our current trajectory before we do something about it?")] 
            public float acceptableDestinationRadius = 2f; 
        }

        // ============================================
        [Header("=== ADD-ON COMPONENTS ===")]
        // ============================================ 
        private Animator _animator;
        private PedestrianMover _mover;
        private PedestrianRVO _pedRVO;
        private AgentAttention _agentAttention;
        
        // ============================================
        [Header("=== PEDESTRIAN SETTINGS ===")]
        // ============================================ 
        [SerializeField, Tooltip(
            "A unique identifier for this pedestrian. Used when writing records of this "
            + "pedestrian's trajectory and whatnot.")] 
        private string _agentLabel;
        [SerializeField, Tooltip(
            "A pedestrian's Intention is their current behavioral goal. This modifies "
            + "their behavior to an extent.")] 
        private Intention _intention;
        [SerializeField, Tooltip(
            "This pedestrian's personality. This will be factored into their route "
            + "determination with `RouteManager`.")] 
        private Personality _personality;
        [SerializeField, Tooltip(
            "Should we randomize our personality? Recommended if "
            + "you don't have specific goals with this pedestrian.")] 
        private bool _randomizePersonality = true;
        [SerializeField, Tooltip(
            "Should we disable ourselves upon reaching the end? Set to true if so.")]


        // ============================================
        [Header("=== EVENTS ===")]
        // ============================================ 
        public UnityEvent<Pedestrian> onRouteEnded;

        // ============================================
        [Header("=== ROUTING DATA - Read-Only ===")]
        // ============================================ 
        [SerializeField, Tooltip(
            "A sequence of RouteNodes that defines their desired trajectory. Calculated by "
            + "`RouteManager`, this list slowly empties out as the pedestrian reaches their current "
            + "destination RouteNode, ultimately resulting in an empty or 1-item list by the "
            + "time they reach their final destination. Thus, 0 is their last RouteNode visited "
            + "(AKA) their starting node for the current leg of the trajectory, while 1 is their "
            + "current Routenode sub-destination along the trajectory.")] 
        private List<RouteNode> _routeTrajectory = new();
        [SerializeField] private Vector3 _routeStart;
        [SerializeField] private Vector3 _routeEnd;
        [SerializeField, Tooltip(
            "A position that represents the pedestrian's CURRENT destination along their trajectory. "
            + "It can technically be any position, so it's not a `RouteNode`. This is usually set when "
            + "the next node along their desired trajectory is set or if a temporary destination position "
            + "is to be used. It's also used by `PedestrianManager` when initializing an agent's destination."
        )] 
        private Vector3 _currentDestination;

        // ============================================
        // === GETTERS ===
        // ============================================
        public string agentLabel => _agentLabel; 
        public Personality personality => _personality;
        public Vector3 currentDestination => _currentDestination;


        // ============================================
        // === AWAKE: References & Personality ===
        // This is the first thing that is called, immediately after `PedestrianManager` instantiates this on the dot.
        // We take advantage of this by setting some preliminary references and values, such as our personality.
        // ============================================
        protected override void Awake() {
            // Grab the necessary references for pedestrian components
            _animator = GetComponent<Animator>();
            _mover = GetComponent<PedestrianMover>();
            _pedRVO = GetComponent<PedestrianRVO>();
            _agentAttention = GetComponent<AgentAttention>();

            // Rebind the animator if it exists
            if (_animator != null) _animator.Rebind();

            // Set our agent's label
            if (string.IsNullOrEmpty(_agentLabel)) {
                _agentLabel = this.gameObject.name;
            }

            // Initialize personality. If we intentionally toggled to randomize, then we do so here.
            if (_randomizePersonality) {
                _personality.riskAversion = UnityEngine.Random.Range(0f, 1f);
                _personality.dirtinessAversion = UnityEngine.Random.Range(0f, 1f);
                _personality.crowdednessAversion = UnityEngine.Random.Range(0f, 1f);
                _personality.distanceAversion = UnityEngine.Random.Range(0f, 1f);
                _personality.litterInclination = UnityEngine.Random.Range(0f, 0f);
            }
            
            // For now, we set `_currentDestination` to be our own position.
            _currentDestination = transform.localPosition;
        }

        private void Update() {
            // Handling Movement Trajectories
            switch (_intention) {
                case Intention.TRAVEL:
                    // Traveling entails that the pedestrian wants to get from point A to point B along a trajectory
                    // Do do this, we need to double-check that our path is set. And handle when a SNAFU is happening.
                    ValidateRoute();
                    break;
                /*                case Intention.APPROACH:
                    // Approaching means we've designated that the target wants to actually move to the player.
                    // This requires some additional checks to ensure behavior is valid. And handle when a SNAFU happens.
                    ValidateApproach();
                    break;
                */
            }
            // At the end of the day, we stil need to aniamte our pedestrian. We do so by calling this function.
            AnimatePedestrian();
            // Update our position this frame, if a writer is being used.
            PedestrianWriter.current?.AddPedestrian(Time.frameCount, Time.time, agentLabel, this.transform);
        }

        // Traveling entails that the pedestrian wants to get from point A to point B along a trajectory
        // Do do this, we need to double-check that our path is set. And handle when a SNAFU is happening.
        public void ValidateRoute() {

            // A pedestrian's trajectory is defined as:
            // startPos -> [node1, node2, ...] -> endPos
            // We don't care about startpos. All we care about are:
            // 1. If `_routeTrajectory.Count >= 1`, then we're moving to a `RouteNode`.
            // 2. otherwise, we should be moving to endPos.
            // the Vector3 `_currentDestination` is where the pedestrian is actually moving. So we need to double-check.

            if (
                (_routeTrajectory.Count >= 1 && _routeTrajectory[0].CheckWithinRadius(transform.position))
                || Vector3.Distance(transform.localPosition, _currentDestination) <= _personality.acceptableDestinationRadius
            ) {
                // We've reached our current destination. What we do next depends on `_routeTrajectory`
                if (_routeTrajectory.Count >= 1) {
                    // We're still on a trajectory. Then we should check if we're within reasonable distance to the current node
                    if (!_routeTrajectory[0].CheckWithinRadius(transform.position) ) {
                        // Not. Then we must set `_currentDestination` to the node as a precaution.
                        _currentDestination = transform.parent.InverseTransformPoint(_routeTrajectory[0].position);
                        return;
                    }
                    // At this point, we are within range. So we must fall back and modify `_routeTrajectory` by removing the current node
                    _routeTrajectory.RemoveAt(0);
                    // From here, we must determine the next `_currentDestination`
                    _currentDestination = (_routeTrajectory.Count >= 1) 
                        ? transform.parent.InverseTransformPoint(_routeTrajectory[0].GetRandomHorizontalPosition())
                        : _routeEnd;
                }
                else {
                    // This is a situation where we didn't have a trajectory. Essentially, we've been moving to `endPos` all along.
                    // The next stage is to disable ourselves
                    _mover.optimalVelocity = Vector3.zero;
                    _currentDestination = transform.localPosition;
                    _intention = Intention.IDLE;
                    onRouteEnded?.Invoke(this);
                    return;
                }
            }

            // We're still valid at this point, so...
            _pedRVO.RVOActive = true;
        }

        /*
        // Approaching means we've designated that the target wants to actually move to the player.
        // This requires some additional checks to ensure behavior is valid. And handle when a SNAFU happens.
        public void ValidateApproach() {
            Vector3 p = PlayerTracker.Instance.transform.localPosition;
            Vector3 toPosition = new Vector3(p.x, transform.localPosition.y, p.z);
            Vector3 diff = transform.localPosition - toPosition;
            float stopRadius = 2f;
            
            if (diff.magnitude < stopRadius) {
                _pedRVO.RVOActive = false;
                _mover.optimalVelocity = new Vector3(0, 0, 0);

                //Keep looking towards the user
                Quaternion targetPosition = Quaternion.LookRotation(toPosition - transform.localPosition);
                if(Quaternion.Angle(transform.localRotation, targetPosition) > 45f) {
                    _mover.targetRotation = targetPosition;
                }
            }
            else {
                _pedRVO.RVOActive = true;
                _currentDestination = PlayerTracker.Instance.transform.localPosition - stopRadius * diff.normalized;
            }
        }
        */
        
        public void ResetRoute() {
            Vector3 worldEndPosition = transform.parent.TransformPoint(_routeEnd);
            List<RouteNode> route = RouteManager.Instance.GetRouteFromPositions(transform.position, worldEndPosition, _personality);
            if (route.Count > 0 && route[0].CheckWithinRadius(transform.position)) route.RemoveAt(0);
            if (route.Count > 0 && route[route.Count-1].CheckWithinRadius(worldEndPosition)) route.RemoveAt(route.Count-1);
            SetRoute(transform.position, worldEndPosition, route);
        }

        // `worldStartPos` and `worldEndPos` are expected to be in world position. However, Pedestrians
        // operate within local positions relative to their parent by default.
        // This means we need to convert each of these into positions relative to their parent
        public void SetRoute(Vector3 worldStartPosition, Vector3 worldEndPosition, List<RouteNode> route) {
            _routeStart = transform.parent.InverseTransformPoint(worldStartPosition);
            _routeEnd = transform.parent.InverseTransformPoint(worldEndPosition);
            _routeTrajectory = route;
            // Set current destination
            _currentDestination = (_routeTrajectory.Count >= 1) 
                ? transform.parent.InverseTransformPoint(_routeTrajectory[0].GetRandomHorizontalPosition())
                : _routeEnd;
        }
        public void SetCurrentDestination(Vector3 worldDestination) {
            _currentDestination = transform.parent.InverseTransformPoint(worldDestination);
        }
        public void SetIntention(Intention i) {
            _intention = i;
        }





        //Procedurally update the animation on the pedestrian according to its current motion
        private void AnimatePedestrian() {
            if (_animator == null) return;
            float forward = _mover.currentVelocity.magnitude;
            float turn = Mathf.Clamp(_mover.rotateDegrees, -1.0f, 1.0f);
            _animator.SetFloat("Forward", forward * 0.3f, 0.1f, Time.deltaTime);
            _animator.SetFloat("Turn", turn, 0.5f, Time.deltaTime);
        }

        /*
        public void OnTriggerEnter(Collider other) {
            if (other.CompareTag("RerouteTrigger")) {
                List<RouteNode> route = RouteManager.Instance.GetRoute(_routeTrajectory[0], _routeDestination, _personality);
                SetRoute(route);
            }
        }
        */

        protected void OnEnable() {
            // Add our pedestrian to the Pedestrian KDTree, if it's being used.
            //PedestrianKDTree.Instance?.AddObstacle(this);
            PedestrianTree.Instance?.TryAddEntity(this);
        }
        /*
        protected void OnDisable() {
            // Remove our pedestrian from the Pedestrian KDTree, if it's being used.
            PedestrianKDTree.Instance?.RemoveObstacle(this);
        }
        */
    }
}