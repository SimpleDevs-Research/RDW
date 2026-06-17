using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace StreetSim {
    public class ObjectSensor : MonoBehaviour
    {
        // Start is called before the first frame update
        public AgentHeadTurn agentHeadTurn;
        public AgentAttention agentAttention;
        public float minimumDotProd = 0.1f;
        public List<Collider> ignoreColliders;

        bool active = false;

        float debug_audiolevel;
        void Start()
        {

            foreach(Collider collider in ignoreColliders)
            {
                Physics.IgnoreCollision(GetComponent<Collider>(), collider);
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            active = true;
        }
        public void OnTriggerStay(Collider other)
        {
            if (!active) return;
            float rotation = Vector3.SignedAngle(transform.forward, agentHeadTurn.lookDir, -transform.right);

            Vector3 distance = other.transform.position - agentHeadTurn.lookSource;
            float dotProd = Vector3.Dot(agentHeadTurn.lookDir.normalized, distance.normalized);

            //Debug.DrawRay(agentHeadTurn.lookSource, distance);

            ObjectOfAttention OOA = other.GetComponent<ObjectOfAttention>();
            //If object is within sight and has any attention priority at all, add it to registered objects

            if (OOA.senseType == SenseType.VISUAL)
            {
                if (
                    dotProd >= minimumDotProd
                    && OOA != null
                    && !agentAttention.objectsInSight.Contains(OOA)
                    && OOA.GetAttentionPriority(agentAttention.transform) > 0)
                {
                    agentAttention.objectsInSight.Add(other.GetComponent<ObjectOfAttention>());
                    //Debug.Log("I am looking");
                }
                //If object is out of sight or has no attention priority, ignore it.
                if (OOA != null && ((dotProd < minimumDotProd && agentAttention.objectsInSight.Contains(OOA)) || OOA.GetAttentionPriority(agentAttention.transform) <= 0))
                {
                    agentAttention.objectsInSight.Remove(other.GetComponent<ObjectOfAttention>());
                }
        }
            else if (OOA.senseType == SenseType.AUDIO)
            {
                debug_audiolevel = OOA.GetAttentionPriority(agentAttention.transform);
                if (OOA.GetAttentionPriority(agentAttention.transform) > 0)
                {
                    agentAttention.objectsInSight.Add(other.GetComponent<ObjectOfAttention>());
                }
                //If object is out of sight or has no attention priority, ignore it.
                if (OOA.GetAttentionPriority(agentAttention.transform) <= 0)
                {
                    agentAttention.objectsInSight.Remove(other.GetComponent<ObjectOfAttention>());
                }
            }

        }

        #if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            Handles.Label(transform.position, "Effective Audio: " + debug_audiolevel.ToString());
        }
        #endif

        public void OnTriggerExit(Collider other)
        {
            ObjectOfAttention OOA = other.GetComponent<ObjectOfAttention>();
            if(OOA != null && agentAttention.objectsInSight.Contains(OOA))
            {
                agentAttention.objectsInSight.Remove(other.GetComponent<ObjectOfAttention>());
            }

        }
    }
}