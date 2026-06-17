using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace StreetSim {
    // This script handles the physical locomotion of the agent, as prescribed by other components, most importantly PedestrianRVO. Consider it the lowest level of pedestrian navigation
    public class PedestrianMover : MonoBehaviour
    {
        [HideInInspector] public Vector3 optimalVelocity;
        [HideInInspector] public Vector3 currentVelocity;
        [HideInInspector] public Quaternion targetRotation;
        [HideInInspector] public float rotateDegrees; //For the animator

        [Header("=== Movement Settings ===")]
        [SerializeField, Tooltip("The bounds of the angular acceleration. Recommended is 90")] 
        private RandomFloat m_maxAngularAcceleration = new RandomFloat(90f);
        [SerializeField, Tooltip("The weight of the max angular acceleration based on the ratio between current velocity and maximum possible velocity.")] 
        private AnimationCurve m_angularAccelerationCurve;
        [Space]
        [SerializeField, Tooltip("How slow or fast can a person rotate their movement? Recommended ~ 145")] 
        private RandomFloat m_maxAngularSpeed = new RandomFloat(160f);
        [SerializeField] 
        private AnimationCurve m_AngularSpeedCurve;
        [Space]
        [SerializeField] private float m_angularSpeed = 0f;
        [SerializeField] private float m_maxPossibleSpeed = 5f; //Used for evaluating turning speed along the curve
        [Space]
        [SerializeField] private RandomFloat m_maxAngularSpeedStanding = new RandomFloat(90f);
        [SerializeField] private RandomFloat m_translateAcceleration = new RandomFloat(2f);

        public Vector3 localDestination => GetComponent<PedestrianRVO>().m_localDestination;

        private void Awake() {
            m_maxAngularSpeed.Randomize();      // How fast do we turn?
            m_translateAcceleration.Randomize();    // How fast do we increase the agent's velocity?
        }

        private void LateUpdate() {
            float targetAngularSpeed = 
                m_AngularSpeedCurve.Evaluate( Mathf.Clamp(currentVelocity.magnitude / m_maxPossibleSpeed, 0.0f, 1.0f) ) 
                * m_maxAngularSpeed;
            float targetAngularAcceleration = 
                m_angularAccelerationCurve.Evaluate( Mathf.Clamp(currentVelocity.magnitude / m_maxPossibleSpeed, 0.0f, 1.0f) ) 
                * m_maxAngularAcceleration;

            // Rotate the agent to face the direction of the optimal velocity,. but only if the optimal velocity isn't Vector3.zero
            if (GetComponent<PedestrianRVO>().RVOActive) {
                targetRotation = (optimalVelocity != Vector3.zero)
                    ? Quaternion.LookRotation(optimalVelocity)
                    : Quaternion.LookRotation(localDestination - transform.position);
            }

            Quaternion previousRotation = transform.rotation;

            float angleDifference = Quaternion.Angle(transform.rotation, targetRotation);
            float angularStep = targetAngularSpeed * Time.deltaTime;

            if (angularStep > angleDifference) targetAngularSpeed = 0;

            m_angularSpeed = Mathf.MoveTowards(m_angularSpeed, targetAngularSpeed, targetAngularAcceleration * Time.deltaTime);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_angularSpeed * Time.deltaTime);

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
            transform.position += transform.forward * currentVelocity.magnitude * Time.deltaTime;

            // Update the animator based on the magnitude of the current velocity
            //KeepInMesh();
        }

        private void KeepInMesh() {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 1f, NavMesh.AllAreas)) transform.position = hit.position;
        }
    }
}