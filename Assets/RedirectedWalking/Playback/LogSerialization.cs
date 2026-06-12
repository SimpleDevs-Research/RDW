using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace RDW {

    [System.Serializable]
    public struct State {
        // =================
        // Timestamps
        // =================
        public int frame;           // Frame number
        public float timestamp;     // Time since beginning of the session
        public float deltaTime;     // Time difference from the previous frame

        // =================
        // Player's Raw Pose in World Space
        // =================
        public Vector3 playerWorldPosition;     // Player's raw position in world space
        public Vector3 playerWorldForward;      // Player's raw forward in world space
        public Quaternion playerWorldRotation;  // Player's raw rotation in world space
        
        // =================
        // Player's Pose in Play Space
        // =================
        public Vector3 playerPlaySpacePosition;     // Player's position relative to play space
        public Vector3 playerPlaySpaceForward;      // Player's forward relative to play space
        public Quaternion playerPlaySpaceRotation;  // Player's rotation relative to play space

        // =================
        // Player's Pose in Env. Space
        // =================
        public Vector3 playerEnvPosition;           // Player's position relative to the environment
        public Vector3 playerEnvForward;            // Player's forward relative to the environment
        public Quaternion playerEnvRotation;        // Player's rotation relative to the environment

        // =================
        // Environment Transformation
        // =================
        public Vector3 envPosition;         // Environment's position in world space
        public Quaternion envRotation;      // Environment's rotation in world space

        // =================
        // Gain Module Frame-to-Frame Details
        // =================
        public bool curvatureActive;        // Was curvature gain active this frame?
        public float curvatureContribution; // How much did curvature gain contribute?
        public bool rotationActive;         // Was rotational gain active this frame?
        public float rotationContribution;  // How much did rotation gain contribute?
        public bool saccadeActive;          // Was saccade gain active this frame?
        public float saccadeContribution;   // How much did saccade gain contribute?
        public bool manualActive;           // Was manual gain active this frame?
        public float manualContribution;    // How much did manual gain contribute?
        public float finalContribution;     // Was was the accumulated gain this frame?
        public Vector3 pivot;               // What was the world position of the pivot?
        public string playerBoundaryState;  // Was the player within, approaching, at the edge, or outside the boundary?
        public string playerTranslating;    // Was the player moving or stationary this frame?
        public string playerTurning;        // Wast he player turning left, right, or not turning this frame?
    }

    [System.Serializable]
    public class Session {
        
        // =================
        // Session Specifics
        // =================
        public string participantID;    // Participant's unique ID
        public string sessionTimestamp; // Datetime when the session was conducted
        public float sessionStart;      // The time since the beginning of the game when the session starts
        public float sessionEnd;        // The time since the beginning of the game when the session ends
        public float duration;          // How long the session took to complete
        public string sceneName;        // The name of the scene that was loaded
        
        // =================
        // Play Space & Boundary Details
        // =================
        public Vector3 worldCenter;         // The world location of the play space, derived from Boundary
        public Vector2 playSpaceSize;       // The XZ plane scale of the play space, derived from Boundary
        public float boundaryApproachDist;  // The distance the user was comfortable approaching the Boundary edge
        public float headPoseOffset;        // The local space offset of the detected head pose
        
        // =================
        // Realtime Data
        // =================
        public List<State> sessionData = new List<State>(); // Each frame data
    }
}
