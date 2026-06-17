using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {
    
    // ======================================
    // === DEMOGRAPHIC GROUP ===
    // A demographic "group" is merely a list of agent pedestrian prefabs that are associated with 
    // that demographic group. Nothing much beyond that.
    // We designate this as a demographic group because some groups and their prefabs might be used 
    // across multiple presets.
    // ======================================

    [CreateAssetMenu(fileName = "Demographic", menuName = "StreetSim/Demographic", order = 2)]
    public class Demographic : ScriptableObject
    {
        public string demographicName;
        public Pedestrian[] pedestrians;
    }
}
