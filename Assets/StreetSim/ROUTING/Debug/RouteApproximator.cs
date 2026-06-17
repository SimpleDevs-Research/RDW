using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StreetSim {
    public class RouteApproximator : MonoBehaviour
    {
        private static readonly Vector2Int[] Direction4 = {
            new Vector2Int( 1, 0),  // East
            new Vector2Int(-1, 0),  // West
            new Vector2Int( 0, 1),  // North
            new Vector2Int( 0,-1),  // South
        };
        private static readonly Vector2Int[] Direction8 = {
            new Vector2Int( 1, 0),  // East
            new Vector2Int(-1, 0),  // West
            new Vector2Int( 0, 1),  // North
            new Vector2Int( 0,-1),  // South
            new Vector2Int( 1, 1),  // NorthEast
            new Vector2Int(-1, 1),  // NorthWest
            new Vector2Int( 1,-1),  // SouthEast
            new Vector2Int(-1,-1)   // SouthWest
        };
        public enum CellType {
            Endpoint,
            Corridor,
            Junction
        }
        public class LobeResult
        {
            public int LobeCount;
            public CellType Type;
            public int NumSamples;
        }

        private struct EdgeKey {
            public Vector2Int a;
            public Vector2Int b;
            public EdgeKey(Vector2Int p1, Vector2Int p2) {
                if (p1.GetHashCode() < p2.GetHashCode()) {
                    a = p1;
                    b = p2;
                }
                else {
                    a = p2;
                    b = p1;
                }
            }
        }

        public class RidgeChain {  
            public List<Vector2Int> cells = new();
        }
        private class Blob {
            public List<Vector2Int> cells = new();
            public Vector2 centroid;
            public Color color;
        }



        [SerializeField] private int width;             // The number of cells along the x-axis
        [SerializeField] private int depth;             // The number of cells along the z-axis
        [SerializeField, Min(0.05f)] public float cellSize = 0.5f;  // How big each cell is in world space
        [SerializeField, Range(1,8)] public int corridorDistanceThreshold = 3;
        [SerializeField, Range(1,8)] public int junctionThreshold = 6;
        [SerializeField, Range(0.1f, 5f)] public float lobeSearchRadius = 0.5f;
        private int prevCorridorDistanceThreshold;
        private int prevJunctionThreshold;
        private float prevLobeSearchRadius;
        [SerializeField] private Vector3 origin;        // bound.min
        [SerializeField] private int maxDistance = 0;

        private bool[,] walkable;      // which cells are walkable (true) or not (false) 
        private int[,] distance;     // for flood-fill approach
        private bool[,] core;       // all nodes that are "core" points (those whose distances match the corridor distance threshold value)
        private List<Blob> blobs = new();
        private bool[,] junction;
        private bool[,] visited;
        private LobeResult[,] lobeResult;

        Dictionary<Vector2Int, int> lookup;
        private List<RidgeChain> chains;


        private void OnDrawGizmos() {
            if (walkable == null) return;

            /*
            Gizmos.color = Color.blue;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (core[x,y]) {
                        Gizmos.DrawSphere(
                            GridToWorld(x,y),
                            cellSize * 0.3f
                        );     
                    }
                }
            }
            */

            /*
            Gizmos.color = Color.yellow;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (junction[x,y]) {
                        Gizmos.DrawSphere(
                            GridToWorld(x,y),
                            cellSize * 0.2f
                        );     
                    }
                }
            }
            */

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    LobeResult lr = lobeResult[x,y];
                    if (lr == null) continue;
                    switch(lr.Type) {
                        case CellType.Endpoint:
                            Gizmos.color = Color.red;
                            break;
                        case CellType.Corridor:
                            Gizmos.color = Color.yellow;
                            break;
                        case CellType.Junction:
                            Gizmos.color = Color.blue;
                            break;
                        default:
                            Gizmos.color = Color.black;
                            break;
                    }
                    Gizmos.DrawSphere(
                        GridToWorld(x,y),
                        cellSize * 0.2f
                    );     
                }
            }
            
            /*
            if (chains != null) {
                Gizmos.color = Color.cyan;
                foreach (RidgeChain chain in chains) {
                    for (int i = 0; i < chain.cells.Count - 1; i++) {
                        Vector3 a = GridToWorld(
                            chain.cells[i].x,
                            chain.cells[i].y
                        );
                        Vector3 b = GridToWorld(
                            chain.cells[i + 1].x,
                            chain.cells[i + 1].y
                        );
                        Gizmos.DrawLine(a, b);
                    }
                }
            }
            

            if (blobs.Count > 0) {
                foreach(Blob blob in blobs) {
                    Gizmos.color = blob.color;
                    foreach(Vector2Int cell in blob.cells) {
                        Gizmos.DrawCube(
                            GridToWorld(cell.x, cell.y),
                            Vector3.one * cellSize * 0.7f
                        );
                    }
                }
            }
            */
        }

        public void Approximate() {

            // Step 1: Grid Formation
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Bounds bounds = new Bounds(tri.vertices[0], Vector3.zero);
            foreach(Vector3 v in tri.vertices) bounds.Encapsulate(v);

            width = Mathf.CeilToInt(bounds.size.x / cellSize);
            depth = Mathf.CeilToInt(bounds.size.z / cellSize);
            walkable = new bool[width, depth];
            origin = bounds.min;

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    Vector3 worldPos = origin + new Vector3( (x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize);
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(worldPos, out hit, cellSize * 0.5f, NavMesh.AllAreas)) {
                        walkable[x, y] = Vector3.Distance(hit.position, worldPos) < cellSize * 0.25f;
                    }
                    else {
                        walkable[x, y] = false;
                    }
                }
            }

            // Step 2: Distance Field via flood fill
            distance = new int[width, depth];
            // Initialization: every walkable cell starts as 0, while every non-walkable cell is set to infinity (or at least the biggest possible value)
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (!walkable[x, y]) {
                        distance[x, y] = 0;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                    else {
                        distance[x, y] = int.MaxValue;
                    }
                }
            }
            // Propagate from Queue
            while (queue.Count > 0) {
                Vector2Int current = queue.Dequeue();
                int currentDistance = distance[current.x, current.y];
                foreach (Vector2Int dir in Direction8) {
                    Vector2Int next = current + dir;
                    if (next.x < 0 || next.x >= width) continue;
                    if (next.y < 0 || next.y >= depth) continue;
                    int candidateDistance = currentDistance + 1;
                    if (candidateDistance < distance[next.x, next.y]) {
                        distance[next.x, next.y] = candidateDistance;
                        queue.Enqueue(next);
                    }
                }
            }

            maxDistance = 0;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (walkable[x, y] && maxDistance < distance[x,y]) maxDistance = distance[x,y];
                }
            }
            Debug.Log($"Maximum distance detected: {maxDistance}");

            // Step 3: Core graph approximation
            core = new bool[width,depth];
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    core[x, y] = walkable[x, y] && distance[x, y] >= corridorDistanceThreshold;
                }
            }            

            // Step 4: Forming core chains
            /*
            chains = ExtractChains(core, width, depth);
            GetChainHealth(core, width, depth);
            */

            // Step 5: Detecting juncture points
            junction = new bool[width,depth];
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (!core[x,y]) continue;
                    int degree = CountNeighbors(core, x, y);
                    junction[x,y] = degree >= junctionThreshold;
                }
            }

            lobeResult = new LobeResult[width,depth];
            int lobeCount = 0;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (!junction[x,y]) continue;
                    lobeResult[x,y] = AnalyzeCell(junction, x, y, width, depth, lobeSearchRadius);
                    if (lobeCount < 10) {
                        Debug.Log(lobeResult[x,y].Type);
                        lobeCount++;
                    }
                }
            }

            /*
            // Step 6: Getting blobs and their average positions
            blobs = new();
            visited = new bool[width,depth];
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < depth; y++) {
                    if (!junction[x,y]) continue;
                    if (visited[x,y]) continue;
                    // Found a new blob
                    Vector2Int start = new Vector2Int(x, y);
                    Blob blob = FloodFillBlob(start);
                    // Estimate blob centroid
                    Vector2 sum = Vector2.zero;
                    foreach(Vector2Int p in blob.cells) {
                        sum += new Vector2(p.x, p.y);
                    }
                    blob.centroid = sum / blob.cells.Count;
                    // Assign random color
                    blob.color = Random.ColorHSV(
                        0f, 1f,
                        0.8f, 1f,
                        0.8f, 1f
                    );
                    // Add blob
                    blobs.Add(blob);
                }
            }
            */
        }

        private Vector3 GridToWorld(int x, int y) {
            return origin + new Vector3( (x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize);
        }
        private Vector2Int WorldToGrid(Vector3 worldPos) {
            Vector3 local = worldPos - origin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize)
            );
        }

        private int CountNeighbors( 
            bool[,] similar,
            int x, int y
        ) {
            int count = 0;
            foreach (Vector2Int dir in Direction8) {
                int nx = x + dir.x;
                int ny = y + dir.y;
                if (nx < 0 || nx >= width) continue;
                if (ny < 0 || ny >= depth) continue;
                if (similar[nx, ny]) count++;
            }
            return count;
        }

        private List<Vector2Int> GetNeighbors(
            bool[,] similar,
            int x, int y
        ) {
            List<Vector2Int> result = new();
            foreach (Vector2Int dir in Direction8) {
                int nx = x + dir.x;
                int ny = y + dir.y;
                if (nx < 0 || nx >= width) continue;
                if (ny < 0 || ny >= depth) continue;
                if (similar[nx, ny]) result.Add(new Vector2Int(nx, ny));
            }
            return result;
        }
        private List<RidgeChain> ExtractChains( 
            bool[,] ridge, int width, int depth
        ) {
            List<RidgeChain> chains = new();
            HashSet<EdgeKey> visitedEdges = new();
            for (int x = 1; x < width - 1; x++) {
                for (int y = 1; y < depth - 1; y++) {
                    if (!ridge[x, y]) continue;
                    int degree = CountNeighbors(ridge, x, y);
                    //
                    // Only start from:
                    // endpoints or junctions
                    //
                    if (degree == 2) continue;
                    Vector2Int start = new Vector2Int(x, y);
                    List<Vector2Int> neighbors = GetNeighbors(ridge, x, y);
                    foreach (Vector2Int next in neighbors) {
                        EdgeKey firstEdge = new EdgeKey(start, next);
                        if (visitedEdges.Contains(firstEdge)) continue;
                        RidgeChain chain = new RidgeChain();
                        chain.cells.Add(start);
                        Vector2Int previous = start;
                        Vector2Int current = next;
                        visitedEdges.Add(firstEdge);
                        while (true) {
                            chain.cells.Add(current);
                            int currentDegree = CountNeighbors( ridge, current.x, current.y);
                            //
                            // stop when we hit
                            // endpoint or junction
                            //
                            if (currentDegree != 2) break;
                            List<Vector2Int> currentNeighbors = GetNeighbors(ridge, current.x, current.y);
                            Vector2Int nextCell = currentNeighbors[0];
                            if (nextCell == previous) nextCell = currentNeighbors[1];
                            EdgeKey edge = new EdgeKey(current, nextCell);
                            visitedEdges.Add(edge);
                            previous = current;
                            current = nextCell;
                        }
                        chains.Add(chain);
                    }
                }
            }
            return chains;
        }

        private void GetChainHealth(
            bool[,] core,
            int width, int depth
        ) {
            int isolated = 0;
            int endpoint = 0;
            int chain = 0;
            int junction = 0;

            for (int x = 1; x < width - 1; x++) { 
                for (int y = 1; y < depth - 1; y++) {
                    if (!core[x,y]) continue;
                    int degree = CountNeighbors(core, x, y);
                    if (degree == 0) isolated++;
                    else if (degree == 1) endpoint++;
                    else if (degree == 2) chain++;
                    else junction++;
                }
            }

            Debug.Log(
                $"isolated={isolated} " +
                $"endpoint={endpoint} " +
                $"chain={chain} " +
                $"junction={junction}");
        }

        private Blob FloodFillBlob(Vector2Int start) {
            Blob blob = new Blob();
            Queue<Vector2Int> queue = new();
            queue.Enqueue(start);
            visited[start.x, start.y] = true;
            while(queue.Count > 0) {
                Vector2Int current = queue.Dequeue();
                blob.cells.Add(current);
                foreach(Vector2Int dir in Direction8) {
                    Vector2Int next = current + dir;
                    if(next.x < 0 || next.x >= width) continue;
                    if(next.y < 0 || next.y >= depth) continue;
                    if(visited[next.x,next.y]) continue;
                    if(!junction[next.x,next.y]) continue;
                    visited[next.x,next.y] = true;
                    queue.Enqueue(next);
                }
            }
            return blob;
        }

        public static LobeResult AnalyzeCell(
            bool[,] core,
            int x, int y,
            int width, int depth,
            float searchRadius,
            float mergeAngleDegrees = 20f)
        {
            List<Vector2> directions = new();
            int radius = Mathf.CeilToInt(searchRadius);
            float innerRadius = searchRadius * 0.75f;
            int n = 0;

            // -------------------------
            // Gather nearby core cells
            // -------------------------

            for (int dx = -radius; dx <= radius; dx++) {
                for (int dy = -radius; dy <= radius; dy++) {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 0 || nx >= width) continue;
                    if (ny < 0 || ny >= depth) continue;
                    if (!core[nx, ny]) continue;

                    float sqrDist = dx * dx + dy * dy;
                    if (sqrDist > searchRadius * searchRadius) continue;
                    if (sqrDist < innerRadius * innerRadius) continue;

                    Vector2 dir = new Vector2(dx, dy).normalized;
                    directions.Add(dir);
                }
            }

            int lobes = CountDirectionalLobes( directions, mergeAngleDegrees);

            CellType type;
            if (lobes <= 1)         type = CellType.Endpoint;
            else if (lobes == 2)    type = CellType.Corridor;
            else                    type = CellType.Junction;

            return new LobeResult {
                LobeCount = lobes,
                Type = type
            };
        }

        private static int CountDirectionalLobes(
            List<Vector2> directions,
            float mergeAngleDegrees
        ) {
            if (directions.Count == 0) return 0;
            float mergeDot = Mathf.Cos(mergeAngleDegrees * Mathf.Deg2Rad);

            List<Vector2> lobes = new();
            foreach (Vector2 dir in directions) {
                bool assigned = false;
                for (int i = 0; i < lobes.Count; i++) {
                    float dot = Vector2.Dot( dir, lobes[i]);
                    if (dot > mergeDot) {
                        lobes[i] = (lobes[i] + dir).normalized;
                        assigned = true;
                        break;
                    }
                }
                if (!assigned) {
                    lobes.Add(dir);
                }
            }

            return lobes.Count;
        }

        private void OnValidate() {
                if (
                    prevCorridorDistanceThreshold != corridorDistanceThreshold
                    || prevJunctionThreshold != junctionThreshold 
                    || prevLobeSearchRadius != lobeSearchRadius
                ) {
                    prevCorridorDistanceThreshold = corridorDistanceThreshold;
                    prevJunctionThreshold = junctionThreshold;
                    prevLobeSearchRadius = lobeSearchRadius;
                    Approximate();
                }
            }

    }
}

