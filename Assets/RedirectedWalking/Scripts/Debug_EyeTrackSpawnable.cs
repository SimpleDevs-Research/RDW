using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Debug_EyeTrackSpawnable : MonoBehaviour
{
    public float closest_distance;
    private void Update()
    {
        closest_distance = int.MaxValue;
        if (Debug_SpawnRandomizer.current == null) return;
        for (int i = 0; i < Debug_SpawnRandomizer.current.num_objects; i++)
        {
            Transform other = Debug_SpawnRandomizer.current.objects[i];
            if (other == this.transform) continue;
            float d = Vector3.Distance(transform.position, other.position);
            if (d < closest_distance) closest_distance = d;
        }
        
    }
}
