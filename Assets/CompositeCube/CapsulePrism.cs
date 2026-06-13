using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CapsulePrism : MonoBehaviour
{
    [Min(0.1f)] public float length = 5f;
    [Min(0.1f)] public float width = 2f;
    [Min(0.1f)] public float height = 2f;

    [Range(4, 64)]
    public int radialSegments = 16;

    [SerializeField]
    private bool invertFaces;

    private Mesh mesh;

    private void Awake() {
        Generate();
    }

    public void Generate() {

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mesh == null) {
            mesh = new Mesh();
            mesh.name = "Capsule Prism";
        }
        else {
            mesh.Clear();
        }

        mf.sharedMesh = mesh;
        BuildMesh();
        BuildColliders();
    }

    private void BuildMesh() {
        float radius = width * 0.5f;

        if (length < width) length = width;

        float straightLength = length - width;

        List<Vector3> profile = new();

        // Left semicircle
        for (int i = 0; i <= radialSegments; i++) {
            float angle = Mathf.PI * 0.5f +
                          Mathf.PI * i / radialSegments;
            profile.Add(new Vector3(
                -straightLength * 0.5f + Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            ));
        }

        // Right semicircle
        for (int i = 0; i <= radialSegments; i++) {
            float angle = -Mathf.PI * 0.5f +
                          Mathf.PI * i / radialSegments;

            profile.Add(new Vector3(
                straightLength * 0.5f + Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            ));
        }

        int ringCount = profile.Count;

        Vector3[] vertices = new Vector3[ringCount * 2];
        Vector3[] normals = new Vector3[ringCount * 2];
        Vector2[] uv = new Vector2[ringCount * 2];

        float halfHeight = height * 0.5f;

        for (int i = 0; i < ringCount; i++) {
            Vector3 p = profile[i];

            vertices[i] = p + Vector3.up * halfHeight;
            vertices[i + ringCount] = p - Vector3.up * halfHeight;

            normals[i] = Vector3.up;
            normals[i + ringCount] = Vector3.down;

            uv[i] = new Vector2(p.x, p.z);
            uv[i + ringCount] = new Vector2(p.x, p.z);
        }

        List<int> triangles = new();

        // Top face
        Vector3 centerTop = Vector3.up * halfHeight;
        int centerTopIndex = vertices.Length;

        // Bottom face
        Vector3 centerBottom = Vector3.down * halfHeight;
        int centerBottomIndex = vertices.Length + 1;

        List<Vector3> finalVerts = new(vertices);
        finalVerts.Add(centerTop);
        finalVerts.Add(centerBottom);

        // Top
        for (int i = 0; i < ringCount; i++)
        {
            int next = (i + 1) % ringCount;

            triangles.Add(centerTopIndex);
            triangles.Add(next);
            triangles.Add(i);
        }

        // Bottom
        for (int i = 0; i < ringCount; i++)
        {
            int next = (i + 1) % ringCount;

            triangles.Add(centerBottomIndex);
            triangles.Add(ringCount + i);
            triangles.Add(ringCount + next);
        }

        // Sides
        for (int i = 0; i < ringCount; i++)
        {
            int next = (i + 1) % ringCount;

            int topA = i;
            int topB = next;

            int botA = i + ringCount;
            int botB = next + ringCount;

            triangles.Add(topA);
            triangles.Add(topB);
            triangles.Add(botA);

            triangles.Add(topB);
            triangles.Add(botB);
            triangles.Add(botA);
        }

        if (invertFaces) {
            for (int i = 0; i < triangles.Count; i += 3)
            {
                (triangles[i + 1], triangles[i + 2]) =
                    (triangles[i + 2], triangles[i + 1]);
            }
        }

        mesh.SetVertices(finalVerts);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void BuildColliders()
    {
        foreach (Collider c in GetComponents<Collider>())
        {
#if UNITY_EDITOR
            DestroyImmediate(c);
#else
            Destroy(c);
#endif
        }

        float radius = width * 0.5f;

        // center box
        BoxCollider box = gameObject.AddComponent<BoxCollider>();

        box.size = new Vector3(
            Mathf.Max(0.01f, length - width),
            height,
            width
        );

        // left cap
        CapsuleCollider left = gameObject.AddComponent<CapsuleCollider>();

        left.direction = 1; // Y
        left.radius = radius;
        left.height = height;

        left.center = new Vector3(
            -(length - width) * 0.5f,
            0f,
            0f
        );

        // right cap
        CapsuleCollider right = gameObject.AddComponent<CapsuleCollider>();

        right.direction = 1;
        right.radius = radius;
        right.height = height;

        right.center = new Vector3(
            (length - width) * 0.5f,
            0f,
            0f
        );
    }
}