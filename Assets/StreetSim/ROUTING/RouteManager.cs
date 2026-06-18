using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using DataStructures.ViliWonka.KDTree;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace StreetSim {

    public enum EnvironmentType {
        Sidewalk,
        Crosswalk
    }

    public class RouteManager : MonoBehaviour
    {
        // ==========================================
        // === STATIC ELEMENTS ===
        // ==========================================
        public static RouteManager Instance;
        [HideInInspector] 
        public string Description = 
            "The RouteManager is a Singleton manager that acts as the simulation's... route manager. "
            + "All that means is that it performs the necessary operations for:\n"
            + "\n1. Initializing all routes in the simulation at the beginning of runtime,"
            + "\n2. Interacting with `PathRegion` colliders placed around the scene to update its routes, and"
            + "\n3. Providing getter functions for pedestrians to query for an optimal path, given their personality + route condition.\n"
            + "\nFor your part, the minimum work you need to do is ensure that `Routes` and a `PathRegion` prefab are set in the Inspector.";


        // ==========================================
        [Header("=== ROUTE MANAGEMENT ===")]
        // ==========================================

        // We need a KDTree - specifically, a DynamicKDTree. Create one, make sure it does NOT 
        // build the tree on Awake, and reference it here.
        [SerializeField, Tooltip("We need a KDTree - specifically, a DynamicKDTree. Create one, make sure it does NOT build the tree on Awake, and reference it here.")]
        private DynamicKDTree _kdTree;

        // You must define the routes within your environment here. You must have at 
        // least one route defined by runtime for this to work.
        [SerializeField, Tooltip("You must define the routes within your environment here. You must have at least one route defined by runtime for this to work.")]
        private List<Route> _routes = new();
        
        // You must set a path region prefab here. This prefab will be used to auto-generete 
        // path regions for each route you've defined above.
        [SerializeField, Tooltip("You must set a path region prefab here. This prefab will be used to auto-generete path regions for each route you've defined above.")]
        private PathRegion _pathRegionPrefab;

        // If you don't want any kind of Dijkstra's logic to path calculation, then toggle 
        // this off. This variable determines whether the path returned is optimal (true) 
        // or naive (false).
        [SerializeField, Tooltip("If you don't want any kind of Dijkstra's logic to path calculation, then toggle this off. This variable determines whether the path returned is optimal (true) or naive (false).")]
        private bool _useDijkstras = true;


        // ==========================================
        [Header("=== SETTINGS: Distance ===")]
        // ==========================================
        #if UNITY_EDITOR
        [Help(
            "How long is the path? This controls whether we care about distance. Longer path -> higher "
            + "computed cost. If we care about distance, this weight should contribute a lot to path "
            + "cost. A higher weight = a stronger consideration of a route's length.", UnityEditor.MessageType.None
        )]
        #endif
        [SerializeField]            private bool _considerDistance = true;
        [SerializeField, Min(0f)]   private float _distanceWeight = 1f;


        // ==========================================
        [Header("=== SETTINGS: Safety ===")]
        // ==========================================
        #if UNITY_EDITOR
        [Help(
            "Safety is... how safe a route or path is. Less safe -> higher computed cost. If we care "
            + "about safety, the safety weight should contribute a lot to path cost. A higher weight = "
            + "a stronger consideration of the safety of a route.", UnityEditor.MessageType.None
        )]
        #endif
        [SerializeField]            private bool _considerSafety = true;
        [SerializeField, Min(0f)]   private float _safetyWeight = 1f;


        // ==========================================
        [Header("=== SETTINGS: Crowd Density ===")]
        // ==========================================
        #if UNITY_EDITOR
        [Help(
            "How dense is a route or path? More dense -> higher computed cost. If we care "
            + "about crowd density, this weight should contribute a lot to path cost. A higher "
            + "weight = a stronger consideration of a route's population.", UnityEditor.MessageType.None
        )]
        #endif
        [SerializeField]            private bool _considerCrowdDensity = true;
        [SerializeField, Min(0f)]   private float _crowdDensityWeight = 1f;


        // ==========================================
        [Header("=== SETTINGS: Dirtiness ===")]
        // ==========================================
        #if UNITY_EDITOR
        [Help(
            "How dirty is a route or path? Dirtier routes -> higher computed cost. If we care "
            + "about dirtiness, this weight should contribute a lot to path cost. A higher "
            + "weight = a stronger consideration of a route's dirtiness.", UnityEditor.MessageType.None
        )]
        #endif
        [SerializeField]            private bool _considerDirtiness = true;
        [SerializeField, Min(0f)]   private float _dirtinessWeight = 1f;


        // ==========================================
        // === PRIVATE ONLY! ===
        // ==========================================
        private List<RouteNode> _nodes = new();
        private HashSet<Transform> _nodeTransforms = new();
        private float[,] _edges;
        public KDTree tree;
        KDQuery query;
        private bool _builtThisFrame = false;
        
        // ==========================================
        // === ... I dunno about these ===
        // ==========================================
        bool drawResults = false;
        float[] resultsSet = new float[0];
        List<int> result_indices = new List<int>();
        

        // =======================================================
        // === AWAKE: Instance + Route + KDTree Initialization ===
        // =======================================================
        private void Awake() {
            // --------------------
            // Set static Intance
            // --------------------
            Instance = this;

            // --------------------
            // Route Initialization
            // --------------------
            RouteNode n1, n2;
            Vector3 n1p, n2p;
            List<Vector3> nodePositions = new();

            foreach(Route route in _routes) {
                // Get references to each node in the current route
                n1 = route.node1;
                n1p = n1.position;  // Note that n1.position effectively queries the localposition of `RouteNode` #1
                n2 = route.node2;
                n2p = n2.position;  // Same as `RouteNode` #2.

                // Add nodes to our nodes lists
                if (!_nodes.Contains(n1)) {
                    _nodes.Add(n1);
                    nodePositions.Add(n1p);
                    _kdTree.TryAddEntity((Entity)n1, false);
                }
                if (!_nodes.Contains(n2)) {
                    _nodes.Add(n2);
                    nodePositions.Add(n2p);
                    _kdTree.TryAddEntity((Entity)n2, false);
                }

                // Update route's distance based on node positions
                route.distance = Vector3.Distance(n1p, n2p);

                // Update the route's nodes' acceptable radii
                n1.acceptableRadius = route.pathWidth;
                n2.acceptableRadius = route.pathWidth;
                
                // We want to generate a path region
                PathRegion pr = Instantiate<PathRegion>(_pathRegionPrefab, transform);
                
                // Modify the path region's position, rotation and local scale
                pr.transform.localPosition = (n1p + n2p) / 2;
                pr.transform.LookAt(n2p);
                pr.transform.localScale = new Vector3(
                    route.pathWidth*2, 
                    1f, 
                    route.distance
                );

                // Initialize the PathRegion
                pr.Initialize();
                
                // Set the route's path region
                route.pathRegion = pr;
            }

            // --------------------
            // Graph Edge Initialization
            // --------------------
            _edges = new float[_nodes.Count, _nodes.Count];
            for (int i = 0; i < _nodes.Count; i++) {
                for (int ii = 0; ii < _nodes.Count; ii++) {
                    _edges[i, ii] = -1;
                }
            }

            // --------------------
            // KDTree Initialization
            // Note that `nodePositions` are consisting of localpositions
            // --------------------
            /*
            query = new KDQuery();
            tree = new KDTree(nodePositions.ToArray(), 32);
            */
            _kdTree.TryUpdatePointCloud();
        }

        // =======================================================
        // === LATE UPDATE: Resetting some things ===
        // Some things (like the flag for whether the KDTree is rebuilt in a frame) need to be reset.
        // We use LateUpdate() to achieve this, after the frame has passed.
        // =======================================================
        private void LateUpdate() {
            _builtThisFrame = false;
        }

        // =======================================================
        // === ROUTE & EDGE COST COMPUTATION ===
        // This is only necessary when an agent needs it. So we only call this whenever
        // `GetRoute` is called. All you need is a reference to the requesting 
        // agent's personality
        // =======================================================
        public void RecomputeRoutes(Pedestrian.Personality personalityData) {
            // We must loop through all our routes and their edges
            foreach(Route route in _routes) {

                // We take up this moment to update ourselves via our PathRegion
                route.dirtiness = route.pathRegion.dirtiness;
                route.safety = route.pathRegion.risk;
                route.density = route.pathRegion.density;
                
                // We now have to calculate the cost of this route.
                route.computedCost = 
                    route.baseCost
                    + (_considerDistance ? 1f : 0f)     * _distanceWeight       * route.distance                    * personalityData.distanceAversion
                    + (_considerCrowdDensity ? 1 : 0)   * _crowdDensityWeight   * route.density * route.distance    * personalityData.crowdednessAversion
                    + (_considerSafety ? 1 : 0)         * _safetyWeight         * route.safety                      * personalityData.riskAversion
                    + (_considerDirtiness ? 1 : 0)      * _dirtinessWeight      * route.dirtiness                   * personalityData.dirtinessAversion;

                // Update the edge associated with this route
                int ind1 = _nodes.IndexOf(route.node1);
                int ind2 = _nodes.IndexOf(route.node2);
                _edges[ind1, ind2] = route.computedCost;
                _edges[ind2, ind1] = route.computedCost;
            }
        }

        // =======================================================
        // === PRIMARY GETTER: Getting the Optimal Route ===
        // This function is primarily called by any pedestrian who needs to recalculate their path.
        // Two variants exist: one with a start and end that are Vector3's, while the other handles RouteNodes.
        // 1. The first variant does an additional step for finding which RouteNode is closest to both the start and end, 
        //      before calling the latter variant. WE EXPECT THESE POSITIONS TO BE IN WORLD SPACE
        // 2. The latter is the true power where we call `RecomputeRoutes()` and do our Dijkstra's calculation.
        // We can actually stop at that point if `_useDijkstras` is FALSE. Doing so will just return the start and end RouteNodes.
        // Otherwise, we do a full Dijkstras implementation to calculate the optimal path based on recomputed route (and edge) costs
        // =======================================================
        public List<RouteNode> GetRouteFromPositions(Vector3 start, Vector3 end, Pedestrian.Personality personalityData) {
            // Calculate the closest RouteNode to `start`
            _kdTree.QueryNearest(start, out int _, out Entity startNodeEntity);
            RouteNode startNode = (RouteNode)startNodeEntity;

            // Calculate the closest RouteNode to `end`
            _kdTree.QueryNearest(end, out int _, out Entity endNodeEntity);
            RouteNode endNode = (RouteNode)endNodeEntity;

            // Call `GetRoute()` but with the proper RouteNodes for the closest start and end route nodes.
            return GetRoute(startNode, endNode, personalityData);
        }
        public List<RouteNode> GetRouteFromCustomNodes(RouteNode start, RouteNode end, Pedestrian.Personality personalityData) {
            // Calculate the closest RouteNode to `start`
            _kdTree.QueryNearest(start.position, out int _, out Entity startNodeEntity);
            RouteNode startNode = (RouteNode)startNodeEntity;

            // Calculate the closest RouteNode to `end`
            _kdTree.QueryNearest(end.position, out int _, out Entity endNodeEntity);
            RouteNode endNode = (RouteNode)endNodeEntity;

            // Call `GetRoute()` but with the proper RouteNodes for the closest start and end route nodes.
            List<RouteNode> predictedRoute = GetRoute(startNode, endNode, personalityData);

            // Self-insert our start and end as new nodes
            predictedRoute.Insert(0, start);
            predictedRoute.Add(end);

            // Return new route;
            return predictedRoute;
        }
        // `start` and `end` are expected to be part of our existing routes
        public List<RouteNode> GetRoute(RouteNode start, RouteNode end, Pedestrian.Personality personalityData) {
            // We update the route costs based on the requesting agent's personality
            RecomputeRoutes(personalityData);

            // Initialize the return List. And if we don't want to use Dijkstra's, we just end it here.
            List<RouteNode> bestPath = new();
            if (!_useDijkstras) {
                bestPath.Add(start);
                if(start != end) 
                    bestPath.Add(end);
                return bestPath;
            }

            // We want to do Dijstra's. At this point, we assume you know what Dijkstra's algorithm does.
            // We won't comment further on this.

            float[] minimumDistance = new float[_nodes.Count];
            for (int i = 0; i < _nodes.Count; i++) minimumDistance[i] = int.MaxValue;
            float[] distances = new float[_nodes.Count];
            for (int i = 0; i < _nodes.Count; i++) distances[i] = int.MaxValue;

            int[] prevNode = new int[_nodes.Count];
            for (int i = 0; i < _nodes.Count; i++) prevNode[i] = -1;
            distances[_nodes.IndexOf(start)] = 0;
            int failsafe1 = 0;

            while (infCount(minimumDistance) > 0 && failsafe1 < 99) {
                failsafe1++;
                
                float currentBestDistance = int.MaxValue;
                int currentBestNodeInd = -1;
                for (int i = 0; i < distances.Length; i++) {
                    if (distances[i] < currentBestDistance && minimumDistance[i] == int.MaxValue) {
                        currentBestNodeInd = i;
                        currentBestDistance = distances[i];
                    }
                }
                int currentNode = currentBestNodeInd;

                minimumDistance[currentNode] = distances[currentNode];
                for(int i = 0; i < _nodes.Count; i++) {
                    if (_edges[currentNode, i] == -1) continue;
                    float possibleNewDistance = distances[currentNode] + _edges[currentNode, i];
                    if(possibleNewDistance < distances[i]) {
                        distances[i] = possibleNewDistance;
                        prevNode[i] = currentNode;
                    }
                }
            }
    
            bestPath.Add(end);
            int currentNodeOnBestPath = prevNode[_nodes.IndexOf(end)];

            int failsafe2 = 0;
            while(currentNodeOnBestPath != -1 && failsafe2 < 10) {
                failsafe2++;
                bestPath.Insert(0, _nodes[currentNodeOnBestPath]);
                currentNodeOnBestPath = prevNode[currentNodeOnBestPath];
            }

            resultsSet = minimumDistance;
            drawResults = true;

            return bestPath;
        }

        int infCount(float[] array) {
            int count = 0;
            for(int i = 0; i < array.Length; i++)
            {
                if (array[i] == int.MaxValue) count++;
            }
            return count;
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected() {

            foreach (Route route in _routes) {
                if(
                    route.node1 != null 
                    && route.node1.transform != null
                    && route.node2 != null
                    && route.node2.transform != null
                ) {

                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(route.node1.position, Vector3.one*0.5f);
                    Gizmos.DrawCube(route.node2.position, Vector3.one*0.5f);

                    Gizmos.color = Color.red;

                    float dist = Vector3.Distance(route.node1.position, route.node2.position);
                    Vector3 routeCenter = route.node1.position + (route.node2.position - route.node1.position) / 2;

                    Gizmos.DrawLine(route.node1.position, route.node2.position);
                    Handles.Label(routeCenter, route.baseCost.ToString());

                    Gizmos.color = Color.blue;

                    Vector3 dir = (route.node2.position - route.node1.position).normalized;
                    Vector3 perpendicular = new Vector3(-dir.z, 0f, dir.x);
                    Vector3 offset = perpendicular * route.pathWidth;

                    Gizmos.DrawLine(
                        route.node1.position + offset,
                        route.node2.position + offset
                    );

                    Gizmos.DrawLine(
                        route.node1.position - offset,
                        route.node2.position - offset
                    );
                }
            }

            if(drawResults) {
                for(int i = 0; i < _nodes.Count; i++) {
                    Handles.Label(_nodes[i].position, resultsSet[i].ToString());
                }
            }
        }
        #endif

        /*
        // =======================================================
        // === KDTREE SHENANIGANS ===
        // We use a KDTree to query for RouteNodes based on query positions and query radii.
        // Any component can use this to call the closest node in the graph connected to Dijkstra's, for example.
        // As a blanket, we just leave this description here and don't touch anything else.
        // =======================================================
        public void FillAndBuild() {
            for (int i = 0; i < _nodes.Count; i++) {
                tree.Points[i] = _nodes[i].position;
            }
            tree.Rebuild();
            _builtThisFrame = true;
        }

        public void DoRadiusQuery(Vector3 queryPosition, float queryRadius, List<int> resultIndices) {
            if (!_builtThisFrame) {
                FillAndBuild();
            }
            query.Radius(tree, queryPosition, queryRadius, resultIndices);
        }
        public void KNearestQuery(Vector3 queryPosition, int k, List<int> resultIndices) {
            if (!_builtThisFrame) {
                FillAndBuild();
            }
            query.KNearest(tree, queryPosition, k, resultIndices);
        }

        public RouteNode GetNearestNode(Vector3 position) {
            List<int> resultIndices = new List<int>();
            KNearestQuery(position, 1, resultIndices);
            return _nodes[resultIndices[0]];
        }
        */

    }
}