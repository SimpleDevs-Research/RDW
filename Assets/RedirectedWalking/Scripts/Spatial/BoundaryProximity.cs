using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundaryProximity : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Material boundaryMaterial;

    private static readonly int PlayerPositionID =
        Shader.PropertyToID("_PlayerPosition");
    private static readonly int WarningDistanceID = 
        Shader.PropertyToID("_WarningDistance");

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

    public void SetWarningDistance(float distance, float multiplier = 1f) {
        boundaryMaterial.SetFloat(
            WarningDistanceID,
            distance * multiplier
        );
    }
}
