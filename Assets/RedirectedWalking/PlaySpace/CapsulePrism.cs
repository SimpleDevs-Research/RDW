using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class CapsulePrism : MonoBehaviour
{
    public enum MeshOrigin { Center, Floor }

    [HideInInspector] 
    public string Description = 
            "This is a custom mesh that creates a rectangular prism; the edges connecting the top and bottom faces are beveled. "
            + "This mesh is dynamically sized and will be auto-rebuilt whenever you change one of the following:\n"
            + "\n- Changing the transform's local scale,"
            + "\n- Changing the bevel radius,"
            + "\n- Smoothness of the bevel,"
            + "\n- Whether the top and bottom faces are generated,"
            + "\n- Inversion of the mesh normals, and"
            + "\n- Whether the mesh's origin is at the bottom or center."
            + "\n\nDon't mess with the `convex` parameter of the attached Mesh Collider; this script will modify that parameter automatically.";


    [Header("=== Capsule Prism ===")]
    [Min(0f), Tooltip("The radius of the corner bevel, in world-scale meters. If set to 0, the mesh becomes a normal cube; if set to the maximum allowed radius, it becomes a cylinder. Dynamically changeable during runtime.")]
    public float cornerRadius = 0.5f;
    private float prevCornerRadius;
    [Range(4, 64), Tooltip("Smoothness of the curved bevel. The higher the number, the smoother it is. Dynamically changeable during runtime.")]
    public int radialSegments = 16;
    private int prevRadialSegments;
    [SerializeField, Tooltip("Should the top and bottom faces be generated?")]
    private bool onlySides = true;
    private bool prevOnlySides;
    [SerializeField, Tooltip("Should the normals be facing inward (which also means you can do raycast checks from inside)? Dynamically changeable during runtime.")]
    private bool inverted = true;
    private bool prevInverted;
    [SerializeField, Tooltip("Should the mesh's origin be at the bottom or at the center of the mesh?")]
    public MeshOrigin meshOrigin = MeshOrigin.Floor;
    private MeshOrigin prevMeshOrigin;

    // If the user changes the local scale of this object during runtime, we have to recalculate too
    private Vector3 prevScale;

    // We need references to this game object's MeshFilter, MeshCollider, and Mesh
    private MeshFilter mf;
    private MeshCollider mc;
    private Mesh mesh;

    // Private values; nobody else needs to gain access to this.
    private List<Vector3> profile;
    private float maxRayDistance;

    // ==============================
    // AWAKE: initial... initialization
    // Grab references, generate mesh, and indicate we're awake
    // ==============================
    private void Awake() {
        // Get References
        mf = GetComponent<MeshFilter>();
        mc = GetComponent<MeshCollider>();
        // Generate the mesh first
        Generate();
    }

    // ==============================
    // GENERATE MESH
    // We initialize the mesh vertices and triangles. We also store the previous settings used for mesh generation
    // ==============================
    public void Generate() {

        // Unset mesh references
        mf.sharedMesh = null;
        mc.sharedMesh = null;
        
        // Initialize mesh. Either create a new one or use an existing one.
        if (mesh == null) {
            mesh = new Mesh();
            mesh.name = "Capsule Prism";
        }
        else {
            mesh.Clear();
        }

        // We do some quick validation of values just to be safe
        Validate();

        // Cache the previous settings; if inspector or runtime values change, they'll get detected in `LateUpdate()`.
        prevScale = transform.localScale;
        prevCornerRadius = cornerRadius;
        prevRadialSegments = radialSegments;
        prevOnlySides = onlySides;
        prevInverted = inverted;
        prevMeshOrigin = meshOrigin;

        // We build the mesh vertices and triangles
        BuildMesh();

        // We set refernces to our mesh to both our MeshFilter and MeshCollider.
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
    }

    // ==============================
    // BUILDING THE MESH
    // Solely called by `Generate()`. Generates the vertices of the mesh in (1,1,1) normalized space while incorporating
    // the world radius and other settings
    // ==============================
    private void BuildMesh() {

        // -------------------
        // Building Mesh Dimensions
        // -------------------

        // Calculate the width (X) and depth (Z) of this from local scale
        float width = Mathf.Abs(transform.localScale.x);
        float depth = Mathf.Abs(transform.localScale.z);

        // Now we must normalize to (1,1,1). This is because meshes are meant to be normalized to (1,1,1) scale.
        // This means that at this point, we no longer care about the world width, depth, height (Y), and cornerRadius of this prism.
        float radiusX = cornerRadius / width;
        float radiusZ = cornerRadius / depth;
        float halfWidth = 0.5f;
        float halfDepth = 0.5f;
        float halfHeight = 0.5f;

        // We must consider the user's preference for mesh origin
        float heightOffset = (meshOrigin == MeshOrigin.Center) ? -halfHeight : 0f;

        // -------------------
        // Inner Rectangle Definition
        // -------------------
        float innerX = halfWidth - radiusX;
        float innerZ = halfDepth - radiusZ;

        // -------------------
        // Building Profile + rounded perimeter
        // Add the four arcs at the four vertical corners. This will create a CCW rounded rectangle perimeter.
        // -------------------
        profile = new List<Vector3>();
        // If the radius is 0 in either end, we just set the corners. Otherwise, we use `AddArc()`.
        if (Mathf.Approximately(radiusX, 0f) || Mathf.Approximately(radiusZ, 0f)) {
            profile.Add(new Vector3( 0.5f, 0f,  0.5f));
            profile.Add(new Vector3(-0.5f, 0f,  0.5f));
            profile.Add(new Vector3(-0.5f, 0f, -0.5f));
            profile.Add(new Vector3( 0.5f, 0f, -0.5f));
        }
        else {
            AddArc( new Vector2( innerX,  innerZ), 0f, radiusX, radiusZ, heightOffset, ref profile );
            AddArc( new Vector2(-innerX,  innerZ), 90f, radiusX, radiusZ, heightOffset, ref profile );
            AddArc( new Vector2(-innerX, -innerZ), 180f, radiusX, radiusZ, heightOffset, ref profile );
            AddArc( new Vector2( innerX, -innerZ), 270f, radiusX, radiusZ, heightOffset, ref profile );
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // -------------------
        // Extruding vertically
        // -------------------
        int ringCount = profile.Count;
        Vector3 v;
        for (int i = 0; i < ringCount; i++) {
            v = profile[i];
            v.y = heightOffset;
            vertices.Add(v); // bottom ring
        }
        for (int i = 0; i < ringCount; i++) {
            v = profile[i];
            v.y = 1f + heightOffset;
            vertices.Add(v); // top ring
        }

        // -------------------
        // Side Triangles
        // -------------------
        for (int i = 0; i < ringCount; i++) {
            int next = (i + 1) % ringCount;

            int b0 = i;
            int b1 = next;
            int t0 = i + ringCount;
            int t1 = next + ringCount;

            triangles.Add(b0);
            triangles.Add(t0);
            triangles.Add(t1);

            triangles.Add(b0);
            triangles.Add(t1);
            triangles.Add(b1);
        }

        if (!onlySides) {
            // -------------------
            // Bottom
            // -------------------
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, heightOffset, 0f));
            for (int i = 0; i < ringCount; i++) {
                int next = (i + 1) % ringCount;
                triangles.Add(bottomCenter);
                triangles.Add(i);
                triangles.Add(next);
            }
            // -------------------
            // Top
            // -------------------
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 1f+heightOffset, 0f));
            for (int i = 0; i < ringCount; i++) {
                int next = (i + 1) % ringCount;
                triangles.Add(topCenter);
                triangles.Add(next + ringCount);
                triangles.Add(i + ringCount);
            }
        }

        if (inverted) {
            for (int i = 0; i < triangles.Count; i += 3) {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }
        }

        mesh.SetVertices(vertices.ToArray());
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // ==============================
    // ADD ARC
    // A helper function. It generates the vertices of the rounded bevel, given the corner to generate and so on.
    // ==============================
    private void AddArc(
            Vector2 center, 
            float startAngleDeg, 
            float radiusX,
            float radiusZ,
            float heightOffset,
            ref List<Vector3> verts
    ) {
        for (int i = 0; i <= radialSegments; i++) {
            // Define the angle based on radial segments
            float angle = (startAngleDeg + i * 90f / radialSegments) * Mathf.Deg2Rad;
            // Define the profile point
            Vector3 p = new Vector3( 
                center.x + Mathf.Cos(angle) * radiusX, 
                0.5f + heightOffset,
                center.y + Mathf.Sin(angle) * radiusZ
            );
            // Add to vertices
            verts.Add(p);
        }
    }
    
    // ==============================
    // UPDATE
    // This leverages Unity's `LateUpdate()` solely to check when inspector values change during runtime
    // ==============================
    private void LateUpdate() {
        if (
            prevScale != transform.localScale 
            || prevCornerRadius != cornerRadius
            || prevRadialSegments != radialSegments
            || prevOnlySides != onlySides
            || prevInverted != inverted 
            || prevMeshOrigin != meshOrigin
        ) {
            Generate();
        }
    }

    // ==============================
    // VALIDATE
    // Whenever a value gets changed, either in runtime or in the inspector, we have to make sure that
    // the proper values are clamped, calculated, etc.
    // ==============================
    private void OnValidate() { Validate(); }
    private void Validate() {
        // We used `cornerRadius` to define the radius of the beveled corners in world space. 
        // Given this, we want to check that the radius does not get bigger than half of the smaller size.
        cornerRadius = Mathf.Min(cornerRadius, Mathf.Min(transform.localScale.x, transform.localScale.z)*0.5f);

        // When doing raycasting for inward-to-outward closest point detection, we need to capture the maximum 
        // possible horizontal distance the ray can follow.
        maxRayDistance = transform.localScale.magnitude;

        // If we're inverted, then the mesh CANNOT be convex. If NOT inverted, it's optimal to make it convex
        if (mc != null) mc.convex = !inverted;
    }

    // ===========================================================================================
    // ===== PUBLIC FUNCTIONS - ANYBODY CAN USE =====
    // ===========================================================================================

    // ==============================
    // GET CLOSEST POINT
    // Query the closest point on the mesh surface. Note that if you're using this normally (i.e. not inverted),
    // it'll just return the reuslt of `Collider.ClosestPoint()`. If inverted, it'll return the point only along
    // the same horizontal plane
    // ==============================
    public Vector3 GetClosestPoint(Vector3 worldPosition, out Vector3 localPos, out float distance) {

        // Calculate local position
        localPos = transform.InverseTransformPoint(worldPosition);

        // Get the raycast origin and raycast destination
        Vector3 prismPoint = new Vector3(
            transform.position.x, 
            Mathf.Clamp(worldPosition.y, 0f, transform.localScale.y-0.001f), 
            transform.position.z
        );
        Vector3 queryPoint = new Vector3(
            worldPosition.x,
            prismPoint.y,
            worldPosition.z
        );

        // Calculate the displacement between origin and destination based on their difference.
        // This displacement gives us the raw distance between the raycast origin and query point
        // As well as the direction the ray should move toward.
        // Note: if `rawDistance` is 0, then the ray would be nonexistent. So we default to Vector3.forward if so.
        Vector3 diff = queryPoint - prismPoint;
        float rawDistance  = diff.magnitude;
        Vector3 dir = (rawDistance > 0f) 
            ? diff.normalized 
            : Vector3.forward;
        
        // Perform raycast with collider only. If no raycast hit, we default to the query point
        Vector3 raycastPoint = queryPoint;
        float raycastDistance = 0f;
        Ray ray = new Ray(prismPoint, dir);
        if (mc.Raycast(ray, out RaycastHit hit,  float.MaxValue)) {
            raycastPoint = hit.point;
            raycastDistance = hit.distance;
        }

        // Inverted: + = inside, - = outside
        distance = Vector3.Distance(raycastPoint, queryPoint) * Mathf.Sign(raycastDistance - rawDistance);

        // Return closest point
        return raycastPoint;
    }

    public float GetDistanceToBoundary(Vector3 worldPosition, out Vector3 closestPoint) {
        // We need to calculate the closest point, so we need to calculate that
        closestPoint = GetClosestPoint(worldPosition, out Vector3 _, out float distance);
        return distance;
    }
}