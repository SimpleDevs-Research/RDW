using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundaryProximity : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Material boundaryMaterial;

    private static readonly int PlayerPositionID =
        Shader.PropertyToID("_PlayerPosition");

    public void SetPlayer(Transform t) {
        player = t;
    }

    private void Update()
    {
        if (player != null) {
            boundaryMaterial.SetVector(
                PlayerPositionID,
                player.position);
        }
    }
}
