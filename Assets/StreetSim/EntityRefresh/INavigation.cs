using UnityEngine;

namespace StreetSim {
    public interface INavigation
    {
        Vector3 optimalVelocity { get; }
        Vector3 localDestination { get; }

        void Tick();
    }
}
