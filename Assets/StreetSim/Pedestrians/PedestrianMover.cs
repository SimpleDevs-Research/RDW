using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace StreetSim {

    // This script handles the physical locomotion of the agent, as prescribed by other components, 
    // most importantly PedestrianRVO. Consider it the lowest level of pedestrian navigation.

    public class PedestrianMover : MonoBehaviour
    {
        // ==============================================
        [Header("=== REFERENCES ===")]
        // ==============================================
        private PedestrianRVO _pedRVO;

        // ==============================================
        // === CORE INPUTS/OUTPUTS ===
        // We either get these from other components or caclulate them for others to use.
        // ==============================================

        // The velocity the pedestrian WANTS to move. Set by `PedestrianRVO` usually, but technically can be 1
        // set by another component
        [HideInInspector] public Vector3 optimalVelocity;
        // The oreintation the pedestrian WANTS to totate to. Extrapolated from either `optimalVelocity` (if it's non-zero) or the 
        // direction to `localDestination` so that it faces its destination
        [HideInInspector] public Quaternion targetRotation; 
        // The current velocity the pedestrian is actually moving in. AKA, reality...
        [HideInInspector] public Vector3 currentVelocity;
        // This is a bit nuanced. It's the AMOUNT of rotation in the current frame that the pedestrian needs to turn.
        // This is used by Animator to determine how much animation blending is needed to translate between just walking and the 
        // turning animations.
        [HideInInspector] public float rotateDegrees; //For the animator

        // ==============================================
        [Header("=== Movement Settings ===")]
        // ==============================================
        // When we refer to "angular", this is always with respect to turning. There are two kinds of smoothing we need with that.
        [SerializeField, Tooltip(
            "How slow or fast can the pedestrian rotate their body? Recommended ~ 145-160. "
            + "This curve is used to finally calculate a \"target\" angular speed for the current frame." 
        )] 
        private RandomFloat _maxAngularSpeed = new RandomFloat(160f);
        [SerializeField, Tooltip(
            "This curve is evaluated against the ratio between current speed and maximum possible speed. "
            +"\n- Slower speed ratio = left part of curve"
            +"\n- Higher speed ratio = right part of curve"
        )] 
        private AnimationCurve _angularSpeedCurve;
        [Space]
        
        [SerializeField, Tooltip(
            "The bounds of the angular acceleration, which controls how quickly we can reach our intended angular speed. "
            + "Recommended is 90."
        )] 
        private RandomFloat _maxAngularAcceleration = new RandomFloat(90f);
        [SerializeField, Tooltip(
            "The weight of the max angular acceleration based on the ratio between current speed and maximum possible speed."
            +"\n- Slower speed ratio = left part of curve"
            +"\n- Higher speed ratio = right part of curve"
        )]
        private AnimationCurve _angularAccelerationCurve;
        [Space]

        [SerializeField, Tooltip("What's the movement acceleration of translation? Recommended ~ 2f")] 
        private RandomFloat m_translateAcceleration = new RandomFloat(2f);
        
        // ==============================================
        [Header("=== DATA CACHE - Read-Only ===")]
        // ==============================================
        [SerializeField, Tooltip("What's the current frame's calculated angular speed?")] 
        private float _currentAngularSpeed = 0f;


        private void Awake() {
            _pedRVO = GetComponent<PedestrianRVO>();
            _maxAngularSpeed.Randomize();      // How fast do we turn?
            m_translateAcceleration.Randomize();    // How fast do we increase the agent's velocity?
        }

        private void LateUpdate() {
            // We need to query values from RVO first of all
            Vector3 localDestination = _pedRVO.localDestination;
            float maxPossibleSpeed = _pedRVO.maxTranslateSpeed;

            // Now we can calculate our target angular speed and angular acceleratio
            float speedRatio = Mathf.Clamp(currentVelocity.magnitude / maxPossibleSpeed, 0.0f, 1.0f);
            float targetAngularSpeed = _angularSpeedCurve.Evaluate(speedRatio) * _maxAngularSpeed;
            float targetAngularAcceleration = _angularAccelerationCurve.Evaluate(speedRatio) * _maxAngularAcceleration;

            // Rotate the agent to face the direction of the optimal velocity,. but only if the optimal velocity isn't Vector3.zero
            if (GetComponent<PedestrianRVO>().RVOActive) {
                targetRotation = (optimalVelocity != Vector3.zero)
                    ? Quaternion.LookRotation(optimalVelocity)
                    : Quaternion.LookRotation(localDestination - transform.localPosition);
            }

            Quaternion previousRotation = transform.localRotation;

            float angleDifference = Quaternion.Angle(transform.localRotation, targetRotation);
            float angularStep = targetAngularSpeed * Time.deltaTime;

            if (angularStep > angleDifference) targetAngularSpeed = 0;

            _currentAngularSpeed = Mathf.MoveTowards(_currentAngularSpeed, targetAngularSpeed, targetAngularAcceleration * Time.deltaTime);

            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, _currentAngularSpeed * Time.deltaTime);

            rotateDegrees = Vector3.SignedAngle(previousRotation * Vector3.forward, transform.forward, Vector3.up);

            // Calcualte the difference between our current velocity and the optimal velocity
            Vector3 diff = optimalVelocity - currentVelocity;

            // As long as there is a different in the two velocities, we HAVE to translate.
            if (diff.sqrMagnitude > 0f) {
                // Calculate the step needed to add to the current velocity
                Vector3 velStep = diff.normalized * m_translateAcceleration * Time.deltaTime;
                // Increment current velocity based on velStep, except in the case that the velocity step overshoots the optimal velocity
                if (velStep.sqrMagnitude > diff.sqrMagnitude) currentVelocity = optimalVelocity;
                else currentVelocity += velStep;
            }

            // Update the position
            transform.localPosition += transform.forward * currentVelocity.magnitude * Time.deltaTime;

            // Update the animator based on the magnitude of the current velocity
            //KeepInMesh();
        }

        // If you want, you can force the agent to remain within the nav mesh, but this is an EXPENSIVE
        // Operation. For now, it's toggled off, but if you need it you need to uncomment it in `LateUpdate()`.
        private void KeepInMesh() {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.localPosition, out hit, 1f, NavMesh.AllAreas)) transform.localPosition = hit.position;
        }
    }
}