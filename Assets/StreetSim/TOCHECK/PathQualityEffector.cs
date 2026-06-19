using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathQualityEffector : MonoBehaviour
{
    public enum effectType { 
        cleanliness,
        safety
    }
    public effectType myEffect;
    public float effectLevel;
    public float timeToDisappear = 40;
    public bool disappear = true;
    public bool rotate = true;

    public void Start()
    {
        RaycastHit hit;
        if(rotate)
            transform.Rotate(new Vector3(0, Random.Range(0, 360), 0));

        if ((Physics.Raycast(transform.position, -Vector3.up, out hit, 10f)))
        {
            if (hit.distance > 0.3f)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y - hit.distance, transform.position.z);
            }
        }
    }
    public void Update()
    {
        timeToDisappear -= Time.deltaTime;
        if(timeToDisappear <= 0 && disappear)
        {
            Destroy(gameObject);
        }
    }
}
