using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RDW {
    public class Calibration : MonoBehaviour
    {
        [System.Serializable]
        public class Trajectory {
            public List<Vector3> points;
            public Color _color;
            public LineRenderer renderer;
            public Trajectory() {  this.points = new List<Vector3>(); }
        }

        [Header("=== Tracked Anchors ===")]
        public Transform head_ref;
        public Transform left_hand_ref;
        public Transform right_hand_ref;

        [Header("=== Head Calibration ===")]
        public Transform head_pos_ref;
        public bool calibrate_head_on_awake = true;

        [Header("=== Rotation Calibration ===")]
        public List<Trajectory> trajectories = new List<Trajectory>();
        public Trajectory cur_trajectory;
        public bool tracking_trajectory = false;
        private bool prev_tracking_trajectory;
        public LineRenderer lineRendererPrefab;

        private void Awake() {
            // If head_pos_ref is null, then use a primitive to represent it.
            if (head_pos_ref == null) {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head_pos_ref = go.transform;
                head_pos_ref.parent = head_ref;
                head_pos_ref.localPosition = Vector3.zero;
                head_pos_ref.localRotation = Quaternion.identity;
                head_pos_ref.localScale = Vector3.one * 0.05f;
            }
            // If toggled to calibrate on awake, do so
            if (calibrate_head_on_awake) CalibrateHeadPos();
            prev_tracking_trajectory = tracking_trajectory;
        }

        public void CalibrateHeadPos() {
            // Assume that the user's hands exists. If not, then we cannot do anything here.
            if (left_hand_ref == null || right_hand_ref == null || head_pos_ref == null) {
                Debug.Log("Cannot estimate true head displacement because of missing hand refs or head pose ref.");
                return;
            }

            // Get the local positions of both hands
            Vector3 left_localPos = head_ref.InverseTransformPoint(left_hand_ref.position);
            Vector3 right_localPos = head_ref.InverseTransformPoint(right_hand_ref.position);
            // Calculate the Z position of both left and right (via averaging)
            float avg_z = (left_localPos.z + right_localPos.z)/2f;
            head_pos_ref.localPosition = new Vector3(0, 0, avg_z);
        }

        private void Update() {
            if (!tracking_trajectory) return;
            Vector3 p = head_pos_ref.position.Flatten();
            cur_trajectory.points.Add(p);
        }

        public void StartTracking(bool force = false) {
            if (!force && tracking_trajectory) return;    // skip if we're already tracking
            cur_trajectory = new Trajectory();
            cur_trajectory._color = Random.ColorHSV();
            tracking_trajectory = true;
            prev_tracking_trajectory = true;

        }
        public void EndTracking(bool force = false) {
            if (!force && !tracking_trajectory) return;   // skip if we're not even tracking yet
            // Add new line renderer
            LineRenderer renderer = Instantiate(lineRendererPrefab, Vector3.zero, Quaternion.identity) as LineRenderer;
            renderer.positionCount = cur_trajectory.points.Count;
            renderer.SetPositions(cur_trajectory.points.ToArray());
            renderer.materials[0].SetColor("_Color",cur_trajectory._color);
            cur_trajectory.renderer = renderer;
            trajectories.Add(cur_trajectory);
            tracking_trajectory = false;
            prev_tracking_trajectory = false;
        }

        private void OnValidate() {
            if (tracking_trajectory != prev_tracking_trajectory) {
                if (tracking_trajectory) StartTracking(true);
                else EndTracking(true);
            }
            prev_tracking_trajectory = tracking_trajectory;
        }
    }
}
