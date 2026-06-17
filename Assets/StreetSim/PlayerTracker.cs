using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class PlayerTracker : RVOEntity
    {
        public static PlayerTracker Instance;

        protected override void Awake() {
            Instance = this;
            base.Awake();
        }

        protected override void Start() {
            base.Start();
            PedestrianTree.Instance?.TryAddEntity(this);
        }

        private void Update() {
            UpdateRVOData(
                new Vector2(transform.position.x, transform.position.z), 
                new Vector2(transform.forward.x, transform.forward.z), new Vector2(transform.forward.x, transform.forward.z)
            );
        }
    }

}
