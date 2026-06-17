using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StreetSim {

    // So all this does is write the head position for an agent?

    public class PedestrianHeadWriter : MonoBehaviour
    {

        public Pedestrian parent_agent;

        private void Update()
        {
            if (PedestrianWriter.current != null) {
                PedestrianWriter.current.AddPedestrian(
                    Time.frameCount, 
                    Time.time, 
                    $"{parent_agent.agentLabel}_Head", this.transform);
            }
        }
    }
}