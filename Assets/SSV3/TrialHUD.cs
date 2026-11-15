using TMPro;
using UnityEngine;

namespace StreetSim {
    // Small head-locked text readout of TrialRunner progress. Created by TrialRunner each time the
    // trial scene loads, parented to the scene's camera so it moves with the view in XR.
    // Display-only: no Canvas, no raycasting, and the text is only rebuilt when what it shows changes.
    public class TrialHUD : MonoBehaviour
    {
        // Placement relative to the camera: ~1.2 m ahead, lower-left of view.
        private static readonly Vector3 LocalOffset = new Vector3(-0.35f, -0.3f, 1.2f);
        private const float FontSize = 0.6f;

        private TrialRunner _runner;
        private TextMeshPro _text;
        private int _lastKey = int.MinValue;

        public static TrialHUD Create(TrialRunner runner) {
            Transform cam = FindCamera();
            if (cam == null) {
                Debug.LogWarning("[TrialHUD] No camera found; HUD not shown.");
                return null;
            }

            GameObject go = new GameObject("TrialHUD");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = LocalOffset;
            go.transform.localRotation = Quaternion.identity;

            TrialHUD hud = go.AddComponent<TrialHUD>();
            hud._runner = runner;
            hud._text = go.AddComponent<TextMeshPro>();
            hud._text.fontSize = FontSize;
            hud._text.alignment = TextAlignmentOptions.BottomLeft;
            hud._text.color = Color.white;
            hud._text.outlineWidth = 0.2f;
            hud._text.outlineColor = Color.black;
            hud._text.enableWordWrapping = false;
            hud._text.rectTransform.sizeDelta = new Vector2(1f, 0.3f);
            hud._text.rectTransform.pivot = new Vector2(0f, 0f);
            return hud;
        }

        private static Transform FindCamera() {
            if (RDW.Player.Instance != null && RDW.Player.Instance.centerEyeCamera != null)
                return RDW.Player.Instance.centerEyeCamera;
            return Camera.main != null ? Camera.main.transform : null;
        }

        private void LateUpdate() {
            if (_runner == null) return;

            // Build a key from everything displayed; only rebuild the string when it changes
            // (once per second while the timer runs, once per spawn while waiting).
            int seconds = Mathf.CeilToInt(_runner.TimeRemaining);
            // Timer running -> key is the seconds (>= 0). Waiting -> key is the negated active count (< 0).
            // The HUD is recreated each trial, so the trial number doesn't need to be part of the key.
            int key = _runner.TimerStarted ? seconds : -1 - _runner.ActiveCount;
            if (key == _lastKey) return;
            _lastKey = key;

            string header = $"Trial {_runner.TrialNumber}/{_runner.TotalTrials} · {_runner.CurrentAgentCount} agents";
            string status = _runner.TimerStarted
                ? $"{seconds / 60}:{seconds % 60:00} remaining"
                : $"Waiting: {_runner.ActiveCount}/{_runner.TargetActiveCount} active";
            _text.text = header + "\n" + status;
        }
    }
}
