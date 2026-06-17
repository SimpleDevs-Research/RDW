using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    public class PedestrianTree : DynamicKDTree
    {
        public static PedestrianTree Instance;

        protected override void Awake() {
            Instance = this;
            base.Awake();
        }
    }
}
