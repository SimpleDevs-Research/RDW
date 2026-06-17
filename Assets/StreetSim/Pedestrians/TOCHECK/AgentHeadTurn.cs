using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace StreetSim {
    public class AgentHeadTurn : MonoBehaviour
    {
        private Transform headTransform;
        private Animator animator;

        [SerializeField] private float headLookWeight = 0f;
        [SerializeField] private float eyeLookWeight = 0f;

        private Transform headToTargetPivot;
        private Vector3 headToTargetVelocity = Vector3.zero;
        public float headToTargetSmoothTime = 0.3F;

        private Transform eyeToTargetPivot;
        private Vector3 eyeToTargetVelocity = Vector3.zero;
        public float eyeToTargetSmoothTime = 0.3F;

        Transform lEyeTransform;
        Transform rEyeTransform;

        public Vector3 lookSource;
        public Vector3 lookDir;

        public AgentAttention agentAttention;

        public GameObject objectSensorPrefab;

        bool hasEyeBones = false;

        private void Awake() {
            animator = GetComponent<Animator>();
            GameObject headToTargetPivotGameObject = new GameObject("Head To Target Pivot");
            headToTargetPivot = headToTargetPivotGameObject.transform;
            headToTargetPivot.parent = headTransform;
            headToTargetPivot.localPosition = Vector3.zero;

            GameObject eyeToTargetPivotGameObject = new GameObject("Eye To Target Pivot");
            eyeToTargetPivot = eyeToTargetPivotGameObject.transform;
            eyeToTargetPivot.parent = headTransform;
            eyeToTargetPivot.localPosition = Vector3.zero;

            lEyeTransform = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            if (GetComponent<Pedestrian>() != null) {
                headTransform.GetComponent<PedestrianHeadWriter>().parent_agent = GetComponent<Pedestrian>();
            }
            Transform attentionTransform = lEyeTransform;

            if (lEyeTransform != null)
            {
                hasEyeBones = true;
            }
            else
            {
                hasEyeBones = false;
                attentionTransform = headTransform;
            }

            lookDir = attentionTransform.forward;
            lookSource = attentionTransform.position;

            objectSensorPrefab = Resources.Load<GameObject>("Prefabs/VisualObjectSensor");
            ObjectSensor sensor = Instantiate(objectSensorPrefab, attentionTransform).GetComponent<ObjectSensor>();
            agentAttention = GetComponent<AgentAttention>();
            sensor.agentAttention = agentAttention;
            sensor.agentHeadTurn = this;

            objectSensorPrefab = Resources.Load<GameObject>("Prefabs/AudioObjectSensor");
            sensor = Instantiate(objectSensorPrefab, attentionTransform).GetComponent<ObjectSensor>();
            agentAttention = GetComponent<AgentAttention>();
            sensor.agentAttention = agentAttention;
            sensor.agentHeadTurn = this;

        }

        // Update is called once per frame
        void Update() {

            if (agentAttention.currentAttentionLocation == AgentAttention.nullLocation) {
                headToTargetPivot.position = Vector3.SmoothDamp(headToTargetPivot.position, headTransform.position + transform.forward, ref headToTargetVelocity, headToTargetSmoothTime);
                ReduceHeadLookWeight();

                eyeToTargetPivot.position = Vector3.SmoothDamp(eyeToTargetPivot.position, headTransform.position + transform.forward, ref eyeToTargetVelocity, headToTargetSmoothTime);
                ReduceEyeLookWeight();
            } else {
                if(hasEyeBones) {
                    headToTargetPivot.position = Vector3.SmoothDamp(headToTargetPivot.position, agentAttention.currentAttentionLocation, ref headToTargetVelocity, headToTargetSmoothTime);
                    if (agentAttention.fullAttention)
                    {
                        IncreaseHeadLookWeight();
                    }
                    else
                    {
                        IncreaseHeadLookWeightLight();
                    }
                    eyeToTargetPivot.position = Vector3.SmoothDamp(eyeToTargetPivot.position, agentAttention.currentAttentionLocation, ref eyeToTargetVelocity, eyeToTargetSmoothTime);
                    IncreaseEyeLookWeight();
                }
                else
                {
                    headToTargetPivot.position = Vector3.SmoothDamp(headToTargetPivot.position, agentAttention.currentAttentionLocation, ref headToTargetVelocity, headToTargetSmoothTime);
                    IncreaseHeadLookWeight();
                }
            
            }
            
        }
        private void LateUpdate()
        {
            if (hasEyeBones)
            {
                lEyeTransform = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                lEyeTransform.LookAt(lEyeTransform.position + headTransform.forward);
                float rotation = Vector3.SignedAngle(transform.forward, lEyeTransform.forward, -transform.right);
                lEyeTransform.LookAt(eyeToTargetPivot.position);
                lEyeTransform.Rotate(new Vector3(-rotation, 0, 0));
                Vector3 rot = lEyeTransform.localRotation.eulerAngles;

                rot.x = rot.x - 360 * (rot.x > 180 ? 1 : 0);
                rot.y = rot.y - 360 * (rot.y > 180 ? 1 : 0);
                rot.x = Mathf.Clamp(rot.x, -15, 15);
                rot.y = Mathf.Clamp(rot.y, -25, 25);

                lEyeTransform.localRotation = Quaternion.Euler(rot);

                rEyeTransform = animator.GetBoneTransform(HumanBodyBones.RightEye);
                rEyeTransform.LookAt(rEyeTransform.position + headTransform.forward);
                rotation = Vector3.SignedAngle(transform.forward, rEyeTransform.forward, -transform.right);
                rEyeTransform.LookAt(eyeToTargetPivot.position);
                rEyeTransform.Rotate(new Vector3(-rotation, 0, 0));
                rot = rEyeTransform.localRotation.eulerAngles;

                rot.x = rot.x - 360 * (rot.x > 180 ? 1 : 0);
                rot.y = rot.y - 360 * (rot.y > 180 ? 1 : 0);
                rot.x = Mathf.Clamp(rot.x, -15, 15);
                rot.y = Mathf.Clamp(rot.y, -25, 25);

                rEyeTransform.localRotation = Quaternion.Euler(rot);

                lookDir = lEyeTransform.forward;
                lookSource = lEyeTransform.position;
            }

            else
            {
                lookDir = headTransform.forward;
                lookSource = headTransform.position;
            }

            //Debug.DrawLine(lookSource, eyeToTargetPivot.position, Color.red);

        }

        private void ReduceHeadLookWeight() {
            headLookWeight = Mathf.Lerp(headLookWeight, 0, Time.deltaTime * 2.5f);
        }
        private void IncreaseHeadLookWeight() {
            headLookWeight = Mathf.Lerp(headLookWeight, 1, Time.deltaTime * 2.5f);
        }
        private void IncreaseHeadLookWeightLight()
        {
            headLookWeight = Mathf.Lerp(headLookWeight, 0.3f, Time.deltaTime * 2.5f);
        }

        private void ReduceEyeLookWeight()
        {
            eyeLookWeight = Mathf.Lerp(eyeLookWeight, 0, Time.deltaTime * 2.5f);
        }
        private void IncreaseEyeLookWeight()
        {
            eyeLookWeight = Mathf.Lerp(eyeLookWeight, 1, Time.deltaTime * 2.5f);
        }

        private void OnAnimatorIK() {
            if (animator == null) return;
            animator.SetLookAtWeight(headLookWeight);
            animator.SetLookAtPosition(headToTargetPivot.position);
        }
    }

}