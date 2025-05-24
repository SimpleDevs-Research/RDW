using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Randomizer : MonoBehaviour
{
    public static Randomizer current;
    public int num_objects = 1000;
    public Vector3 scene_bounds = new Vector3(25, 25, 25);
    public Vector3 lorenz = new Vector3(28, 10, 8/3);
    public GameObject template;
    public bool use_primitive = true;
    [HideInInspector] public Transform[] objects;

    private void Awake()
    {
        current = this;    
    }

    private void Start()
    {
        GameObject go;
        Transform got;
        objects = new Transform[num_objects];
        for (int i = 0; i < num_objects; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-scene_bounds.x, scene_bounds.x),
                Random.Range(-scene_bounds.y, scene_bounds.y),
                Random.Range(-scene_bounds.z, scene_bounds.z)
            );
            if (use_primitive)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }
            else
            {
                go = Instantiate(template, Vector3.zero, Quaternion.identity);
            }
            got = go.transform;
            got.parent = this.transform;
            got.position = transform.InverseTransformPoint(pos);
            got.localScale = 0.2f * Vector3.one;
            objects[i] = got;
        }
    }
    
    private Vector3 LorenzStep(Vector3 pos) {
        float p = lorenz.x;
        float a = lorenz.y;
        float b = lorenz.z;
        return new Vector3(
            a * (pos.y - pos.x),
            pos.x * (p - pos.z) - pos.y,
            pos.x * pos.y - b * pos.z
        );
    }

    private void Update() {
        for(int i = 0; i < num_objects; i++) {
            Vector3 cp = objects[i].position;
            objects[i].position += LorenzStep(cp) * Time.deltaTime * 0.1f;
        }
    }
}
