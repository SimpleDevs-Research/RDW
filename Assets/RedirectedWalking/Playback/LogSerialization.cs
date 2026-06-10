using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace RDW {

    [System.Serializable]
    public class State {
        public int frame;
        public float timestamp;
        public float delta_time;
        public Vector3 world_position;
        public Quaternion world_rotation;
        public Vector3 env_position;
        public Quaternion env_rotation;
    }

    [System.Serializable]
    public class Session {
        public string session_timestamp;
        public Vector3 world_center_position;
        public Vector3 min_anchor_position;
        public Vector3 max_anchor_position;
        public List<string> gain_modules;
        public List<State> state_data;
    }
}
