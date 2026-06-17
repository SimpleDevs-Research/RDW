using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class PedestrianWriter : MonoBehaviour
    {
        public static PedestrianWriter current;
        public class PedestrianRecord {
            public string _guid;
            public int frame;
            public float timestamp;
            public Vector3 position;
            public Vector3 forward;
            public PedestrianRecord(string _guid, int frame, float timestamp, Vector3 position, Vector3 forward) {
                this._guid = _guid;
                this.frame = frame;
                this.timestamp = timestamp;
                this.position = position;
                this.forward = forward;
            }
        }
        public bool initializeOnStart = true;
        private List<PedestrianRecord> backlog;
        private float startTime;
        public CSVWriter writer;

        private float[] fpsLog = new float[3];
        private float thisDeltaTime;
        private float thisFPSRaw;
        private float thisFPSSmooth;
        private int thisActiveAgents;

        public bool calcDeltas;

        private void Awake() {
            current = this;
        }
        private void Start() {
            if (initializeOnStart) Initialize();
        }

        public void Initialize() {
            startTime = Time.time;
            writer.Initialize();
        }
        private void Update()
        {
            calcDeltas = true;
            thisDeltaTime = Time.deltaTime;

            thisFPSRaw = 1f / Time.deltaTime;

            fpsLog[2] = fpsLog[1];
            fpsLog[1] = fpsLog[0];
            fpsLog[0] = thisFPSRaw;

            thisFPSSmooth = fpsLog[0] * 0.5f + fpsLog[1] * 0.3f + fpsLog[2] * 0.2f;

            thisActiveAgents = -1;
        }

        public void AddPedestrian(int frame, float timestamp, string label, Transform t) {

            writer.AddPayload(frame);
            writer.AddPayload(timestamp-startTime);
            writer.AddPayload(t.gameObject.GetInstanceID());
            writer.AddPayload(label);
            writer.AddPayload(t.position.x);
            writer.AddPayload(t.position.y);
            writer.AddPayload(t.position.z);
            writer.AddPayload(t.forward.x);
            writer.AddPayload(t.forward.y);
            writer.AddPayload(t.forward.z);
            writer.AddPayload(thisDeltaTime);
            writer.AddPayload(thisFPSRaw);
            writer.AddPayload(thisFPSSmooth);
            if (thisActiveAgents == -1) thisActiveAgents = PedestrianManager.Instance.numActivePedestrians;
            writer.AddPayload(thisActiveAgents);
            writer.WriteLine();
        }

        void OnDestroy() {
            writer.Disable();
        }
    }
}