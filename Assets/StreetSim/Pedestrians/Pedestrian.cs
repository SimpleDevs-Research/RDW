using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RVO;

namespace StreetSim {
    public class Pedestrian : RVO.Robot
    {
        // This is an extension of the `RVO.Robot` class.
        // Unlike the basic Robot, Pedestrians need to keep track of a trajectory to follow.
        // Furthermore, Pedestrians distinguish between 'current' and 'final' destination
        // points. 

        [Header("=== Pedestrian References ===")]
        public Animator _animator;
        public LODGroup _lodGroup;
        public List<Renderer> _renderers = new();

        // ============================================
        [Header("=== ROUTING DATA - Read-Only ===")]
        // ============================================ 
        [SerializeField, Tooltip(
            "A sequence of RouteNodes that defines their desired trajectory. Calculated by "
            + "`RouteManager2`, this list slowly empties out as the pedestrian reaches their current "
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

        public bool ValidateRoute() {
            // Two initial checks: 
            // 1) Either we have a trajectory and we just reached it, or
            // 2) We've reached our current destination

            Vector3 pA = generator.vo_op.positions[agent_index];
            bool reached_destination = generator.vo_op.reached_destination[agent_index];

            if (
                (_routeTrajectory.Count >= 1 && _routeTrajectory[0].CheckWithinRadius(pA))
                || reached_destination
            ) {
                // We've reached our current destination. What we do next depends on `_routeTrajectory`
                if (_routeTrajectory.Count >= 1) {
                    // We're still on a trajectory. Then we should check if we're within reasonable distance to the current node
                    if (!_routeTrajectory[0].CheckWithinRadius(pA) ) {
                        // Not. Then we must set the currentDestination to the node position as a precaution.
                        UpdateCurrentDestination(_routeTrajectory[0].position);
                        return true;
                    }
                    // At this point, we are within range. So we must fall back and modify `_routeTrajectory` by removing the current node
                    _routeTrajectory.RemoveAt(0);
                    // From here, we must determine the next `_currentDestination`
                    Vector3 destination = (_routeTrajectory.Count >= 1) 
                        ? _routeTrajectory[0].GetRandomHorizontalPosition()
                        : _routeEnd;
                    UpdateCurrentDestination(destination);
                    return true;
                }
                else {
                    // This is a situation where we didn't have a trajectory. Essentially, we've been moving to `endPos` all along.
                    // The next stage is to disable ourselves
                    return false;
                }
            }

            // We're still good if we reached this point
            return true;
        }

        // `startPos` and `endPos` are expected to be in local position.
        public void SetRoute(Vector3 startPosition, Vector3 endPosition, List<RouteNode> route) {
            // Set local cache
            _routeStart = startPosition;
            _routeEnd = endPosition;
            _routeTrajectory = route;
            // Calculate the current destination
            Vector3 destination = (_routeTrajectory.Count >= 1) 
                ? _routeTrajectory[0].GetRandomHorizontalPosition()
                : _routeEnd;
            // Update Jobs end
            UpdateCurrentDestination(destination);
        }

        public void UpdatePosition(Vector3 localPosition) {
            transform.localPosition = localPosition;                                        // Set the robot's position in both CPU (via local position)
            if (generator != null) generator.vo_op.positions[agent_index] = localPosition;  // And in the Jobs
        }

        // Unlike position, rotation isn't taken into account in Jobs. 
        // So there's no need to update it in `vo_op`.
        public void UpdateRotation(Quaternion localRotation) {
            transform.localRotation = localRotation;
        }

        // Combines UpdatePosition and UpdateRotation
        public void UpdatePose(Vector3 localPosition, Quaternion localRotation) {
            transform.localPosition = localPosition; 
            transform.localRotation = localRotation;
            if (generator != null) generator.vo_op.positions[agent_index] = localPosition;
        }

        public void UpdateCurrentDestination(Vector3 localDestination) {
            _currentDestination = localDestination;
            if (generator != null) {
                generator.vo_op.destinations[agent_index] = localDestination;
                generator.vo_op.reached_destination[agent_index] = false;
            }
        }

        public void UpdateAnimator() {
            if (_animator != null) {
                Vector3 velocity = generator.vo_op.velocities[agent_index];
                float forward = velocity.magnitude;
                _animator.SetFloat("Forward", forward * 0.3f, 0.1f, Time.deltaTime);
            }
        }

        public void ToggleAnimation(bool set_to) {
            if (_animator != null) _animator.enabled = set_to;
            if (_lodGroup != null) _lodGroup.enabled = set_to;
            if (_renderers.Count > 0) foreach(Renderer _renderer in _renderers) _renderer.enabled = set_to;
        }


    }
}
