using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    [RequireComponent(typeof(AudioSource))]
    public class BoundaryAudio : MonoBehaviour
    {
        private AudioSource audioSource;
        
        [Header("=== References ===")]
        [SerializeField, Tooltip("Can be set manually. If `null` or not set, it will attempt to query for one")]
        private Transform player;
        
        private void Awake() {
            audioSource = GetComponent<AudioSource>();
        }

        private void Start() {
            if (player == null && RDW.Instance != null) player = RDW.Instance.headPoseAnchor;
        }

        public void Play() {
            if (!audioSource.isPlaying) audioSource.Play();
        }
        public void Pause() {
            if (audioSource.isPlaying) audioSource.Stop();
        }

        private void Update() {
            // Must have a `Boundary` game object instance in the scene
            if (Boundary.Instance == null || player == null) return;

            // Update this transform's position to match the closest point on the boundary
            float distance = Boundary.Instance.GetDistanceToBoundary(player.position, out Vector3 closestPoint);
            transform.position = player.position + Vector3.Normalize(closestPoint - player.position) * Mathf.Min(distance, 0.5f);
        }


    }
}
