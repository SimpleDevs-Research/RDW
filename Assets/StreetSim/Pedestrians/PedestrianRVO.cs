using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.AI;
using Unity.Jobs;
using Unity.Burst;
using RVO;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StreetSim {

    [Flags]
    public enum RVOLayer {
        Pedestrian = 1,
        User = 2,
    }

    //This script calculates minute movement "segments" along the desired global route using RVO and Navmesh.
    public class PedestrianRVO : RVOEntity
    {
        
        [System.Serializable]
        public struct DirData {
            public int index;
            public float2 direction;
            public float base_penalty;
            public float time_cost;
            public float penalty => base_penalty + time_cost;
            public DirData(int index, Vector2 direction, float base_penalty=0f, float time_cost=0f) {
                this.index = index;
                this.direction = (float2)direction;
                this.base_penalty = base_penalty;
                this.time_cost = time_cost;
            }
            public void UpdateTimeCost(float newCost) {
                if (newCost > this.time_cost) this.time_cost = newCost;
            }
        }

        [System.Serializable]
        public struct DirPenalty {
            public int index;
            public float penalty;
            public DirPenalty(int index, float penalty=0f) {
                this.index = index;
                this.penalty = penalty;
            }
        }

        [Header("=== References ===")]
        [SerializeField] private Pedestrian _pedestrian;
        [SerializeField] private PedestrianMover _pedestrianMover;

        [Header("=== Movement Settings ===")]
        [SerializeField] public bool RVOActive = false;
        [SerializeField] private RandomFloat m_maxTranslateSpeed = new RandomFloat(1.5f, true, new Vector2(1.25f, 1.75f)); //new RandomFloat(1.25f, true, new Vector2(1.25f, 1.75f));
        [SerializeField] public Vector3 m_localDestination;

        [SerializeField] private RandomFloat m_destinationRange = new RandomFloat(0.5f);
        [SerializeField] private RandomFloat m_repathTimeGap = new RandomFloat(1f);
        [SerializeField] private int m_numDirections = 25;
        
        [SerializeField] private RandomFloat m_radiusOfAvoidance = new RandomFloat(0.25f, true, new Vector2(0.2f, 0.4f));
        [SerializeField] private RandomFloat m_aggression = new RandomFloat(0.5f, true, new Vector2(0.25f, 0.75f));
        [SerializeField] private float m_viewRadius = 5f;
    
        [SerializeField] private RVOLayer m_rvoLayer;
        [SerializeField] private RVOLayer m_rvoMask;

        [Header("=== Debug Settings ===")]
        [SerializeField] private bool m_drawPath = false;
        [SerializeField] private bool m_drawSuitableDirections = false;
        [SerializeField] private bool m_scaleViewedPedestrians = false;

        [Header("=== Outcomes - Read Only ===")]
        [SerializeField] private List<Vector3> m_pathPositions;
        private Vector2[] m_directionsTemplate;
        private NativeArray<DirData> m_directionsArray;
        private NativeArray<DirPenalty> m_dirPenaltiesArray;
        private NativeArray<RVOData> m_pedDataArray;
        [SerializeField] private DirPenalty[] m_dirPenalties;
        [SerializeField] private DirPenalty[] m_dirPenaltiesSanple;
        private bool m_jobScheduled = false;
        private DirectionJob m_dirJob;
        private JobHandle m_dirJobHandle;
        private List<DirData> m_suitableDirections = new List<DirData>();
        private NavMeshPath m_navPath;

        [SerializeField] private bool m_showNeighbors = false;
        [SerializeField] private List<Transform> m_gizmos_result_transforms = new List<Transform>();

        #if UNITY_EDITOR
        private void OnDrawGizmos() {
            if (m_drawPath && m_pathPositions.Count == 0) {
                Gizmos.color = Color.blue; 
                Gizmos.DrawLine(transform.position+Vector3.up, m_pathPositions[0]+Vector3.up);
                for(int i = 0; i < m_pathPositions.Count; i++) {
                    Gizmos.DrawSphere(m_pathPositions[i]+Vector3.up, 0.05f);
                    if (i < m_pathPositions.Count-1) Gizmos.DrawLine(m_pathPositions[i]+Vector3.up, m_pathPositions[i+1]+Vector3.up);
                }
            }

            if (m_drawSuitableDirections && m_suitableDirections.Count > 0) {
                Gizmos.color = Color.red;
                foreach(DirData d in m_suitableDirections) {
                    Gizmos.DrawRay(transform.position, d.direction.ToVector3());
                }
            }

            if (m_showNeighbors) {
                Gizmos.color = Color.blue;
                List<int> result_indices = new List<int>();
                m_gizmos_result_transforms = new List<Transform>();
                PedestrianTree.Instance.QueryRadius(transform.position, m_viewRadius, result_indices);

                if (result_indices.Count > 0) {
                    for (int i = 0; i < result_indices.Count; i++) {
                        Pedestrian ped = PedestrianManager.Instance.allPedestrians[result_indices[i]];
                        Gizmos.DrawLine(transform.position, ped.transform.position);
                        m_gizmos_result_transforms.Add(ped.transform);
                    }
                }
            }
        }
        #endif

        protected override void Awake() {
            base.Awake();

            // Get References
            _pedestrian = GetComponent<Pedestrian>();
            _pedestrianMover = GetComponent<PedestrianMover>();

            // Initialize any paraameters that need to be randomized
            m_destinationRange.Randomize();     // Determine how close we want to be to other entities
            m_radiusOfAvoidance.Randomize();    // How close are we willing to be with other entities?
            m_avoidanceRadius = m_radiusOfAvoidance;
            m_aggression.Randomize();           // How aggressive are we with penalized directions?
            m_maxTranslateSpeed.Randomize();             // How fast can we go at max?

            // Initialize the directions
            m_directionsTemplate = RVO.Utils.CreateDirections2D(m_numDirections, m_maxTranslateSpeed);
            m_directionsArray = new NativeArray<DirData>(m_numDirections+1, Allocator.Persistent);
            m_dirPenaltiesArray = new NativeArray<DirPenalty>(m_numDirections+1, Allocator.Persistent);
            m_jobScheduled = false;
            BeginCalculatingSegmentPath();

            // Initialize our pedestrian data
            InitializePedData();
        }

        public void BeginCalculatingSegmentPath()
        {
            // Initialize the path determinator
            StartCoroutine(CalculateSegmentPath());
        }
        private IEnumerator CalculateSegmentPath() {
            m_navPath = new NavMeshPath();
            while(true) {
                if (RVOActive) {
                    //print("repathing towards " + m_destination.ToString());
                    m_pathPositions = new List<Vector3>();
                    bool pathFound = NavMesh.CalculatePath(
                        transform.position,
                        new Vector3(_pedestrian.currentDestination.x, transform.position.y, _pedestrian.currentDestination.z),
                        NavMesh.AllAreas,
                        m_navPath
                    );
                    if (pathFound) {
                        print("pathfound");
                        NavMeshHit hit;
                        foreach (Vector3 p in m_navPath.corners)
                        {
                            if (NavMesh.FindClosestEdge(p, out hit, NavMesh.AllAreas))
                            {
                                if (hit.distance < m_avoidanceRadius) m_pathPositions.Add(hit.position + hit.normal * m_avoidanceRadius);
                                else m_pathPositions.Add(p);
                            }
                        }
                    }
                    else print("failure");
                    yield return new WaitForSeconds(m_repathTimeGap);
                }
                yield return null;
            }
        }


        private void Update() {
            if(transform.localScale.x > 0) {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, 0.1f);
            }
            UpdateOptimalVelocity();
        }
        private void UpdateOptimalVelocity() {
            // Update our pedestrian data
            UpdatePedData();
            if (RVOActive) {
                m_localDestination = UpdateLocalDestination();

                // Initialize the direction and direction penalties arrays. We ensure that the desired direction is also added
                Vector2 vD = new Vector2(m_rvoData.desiredVelocity[0], m_rvoData.desiredVelocity[1]);
                for (int i = 0; i < m_directionsTemplate.Length; i++)
                {
                    Vector2 dir = m_directionsTemplate[i];
                    float diff = (vD - dir).magnitude;
                    m_directionsArray[i] = new DirData(i, dir, diff);
                    m_dirPenaltiesArray[i] = new DirPenalty(i, diff);
                }
                m_directionsArray[m_numDirections] = new DirData(m_numDirections, vD);
                m_dirPenaltiesArray[m_numDirections] = new DirPenalty(m_numDirections);
                m_jobScheduled = false;

                // Now, depending on the update type, we can either update via `UpdateDirection()` (the default) or `UpdateDirectionBurst()` (using burst compiler).
                UpdateDirectionBurst();
            } 
            else {
                m_jobScheduled = false;
            }
        }

        private void InitializePedData() {
            // Calculate the current state of the pedestrian. This includes:
            // 1. The unique instance ID of this component
            // 1. its current position,
            // 2. its current velocity, and
            // 3. its desired velocity (max speed in the direction of its current target)
            int guid = this.GetInstanceID();
            Vector2 pA = transform.position.ToVector2();
            Vector2 vA = _pedestrianMover.currentVelocity.ToVector2();
            Vector2 vD = (m_localDestination - transform.position).ToVector2().normalized * m_maxTranslateSpeed;

            m_rvoData = new RVOData(guid, pA, vA, vD, m_avoidanceRadius, m_rvoLayer);
        }
        private void UpdatePedData() {
            // Calculate the current state of the pedestrian. This includes:
            // 1. its current position,
            // 2. its current velocity, and
            // 3. its desired velocity (max speed in the direction of its current target)
            Vector2 pA = transform.position.ToVector2();
            Vector2 vA = _pedestrianMover.currentVelocity.ToVector2();
            Vector2 vD = (m_localDestination - transform.position).ToVector2().normalized * m_maxTranslateSpeed;
            UpdateRVOData(pA, vA, vD);
        }

        private Vector3 UpdateLocalDestination() {
            // Always return `m_destination` if the we don't have any navmesh paths
            if (m_pathPositions.Count == 0) return _pedestrian.currentDestination;
            bool destinationFound = false;
            while(m_pathPositions.Count > 0 && !destinationFound) {
                destinationFound = Vector3.Distance(m_pathPositions[0], transform.position) > m_destinationRange;
                if (!destinationFound) m_pathPositions.RemoveAt(0);
            }
            if (destinationFound) return new Vector3(m_pathPositions[0].x, 0f, m_pathPositions[0].z);
            else return _pedestrian.currentDestination;
        }

        private void UpdateDirectionBurst() {

            //  1. Convert the list of visible entities into a list of structs. End early if we don't have any pedestrians to consider, or if the way is blocked.
            List<RVOData> pedData = GetPedData();
            LayerMask layerMask = LayerMask.GetMask("NavObstruction");
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, 1.4f, layerMask))
            {
                //DON'T stop if *inside* the obstruction. Just keep going.
                if (!Physics.Raycast(transform.position, transform.TransformDirection(Vector3.back), out hit, 0.1f, layerMask))
                {
                    _pedestrianMover.optimalVelocity = Vector3.zero;
                    m_jobScheduled = false;
                    return;
                }
            }
                    
            if (pedData.Count == 0) {
                _pedestrianMover.optimalVelocity = new Vector3(m_rvoData.desiredVelocity[0], 0f, m_rvoData.desiredVelocity[1]);
                m_jobScheduled = false;
                return;
            }

            //  2. Create the necessary Pedestrian Data NativeArray
            m_pedDataArray = new NativeArray<RVOData>(pedData.ToArray(), Allocator.TempJob);

            //  3. Conduct the job
            m_dirJob = new DirectionJob() {
                directions = m_directionsArray,
                pedData = m_pedDataArray,
                guid = m_rvoData.guid,
                pA = m_rvoData.position,
                vA = m_rvoData.velocity,
                radius = m_rvoData.radius,
                maxSpeed = m_maxTranslateSpeed,
                aggressiveness = m_aggression,
                dirPenalties = m_dirPenaltiesArray
            };
            m_dirJobHandle = m_dirJob.Schedule(m_directionsArray.Length, 16);
            m_jobScheduled = true;
            PerformDirectionJob();
        }
        private void PerformDirectionJob() {
            if (!m_jobScheduled) return;

            //  1. Assuming that we actually have a job listed, we complete it.
            m_dirJobHandle.Complete();

            //  2. Extract the data from `dirPenaltiesArray`, find the optimal velocity
            m_dirPenalties = m_dirJob.dirPenalties.ToArray();
            Array.Sort(m_dirPenalties, (v1,v2)=>v1.penalty.CompareTo(v2.penalty));
            _pedestrianMover.optimalVelocity = m_directionsArray[m_dirPenalties[0].index].direction.ToVector3();
        }
        private List<RVOEntity> GetVisiblePedestrians() {
            List<int> resultIndices = new List<int>();
            PedestrianTree.Instance.QueryRadius(transform.position, m_viewRadius, resultIndices);

            List<RVOEntity> obstacles = new List<RVOEntity>();
            for (int i = 0; i < resultIndices.Count; i++) {
                RVOEntity obstacle = (RVOEntity) PedestrianTree.Instance.entities[resultIndices[i]];
                if (m_scaleViewedPedestrians)  {
                    Debug.DrawLine(transform.position, obstacle.transform.position);
                }

                //Check that the obstacle layer matches with my mask
                if ( (m_rvoMask & obstacle.m_rvoData.rvoLayer) == 0) {
                    //print($"name: {gameObject.name}, mask: {m_rvoMask} & layer: {obstacle.m_rvoData.rvoLayer}, ignore");
                    continue;
                }
                //print($"name: {gameObject.name}, mask: {m_rvoMask} & layer: {obstacle.m_rvoData.rvoLayer}, accept");
                //Check angle, right now it's 45 for easy calculation
                Vector2Int a = new Vector2Int(Mathf.RoundToInt(transform.forward.x*10), Mathf.RoundToInt(transform.forward.z*10));
                Vector2Int b = new Vector2Int( Mathf.RoundToInt((obstacle.transform.position.x - transform.position.x)*10), Mathf.RoundToInt((obstacle.transform.position.z - transform.position.z) * 10));
                int dot = a.x * b.x + a.y * b.y;
                if (dot / (a.magnitude * b.magnitude) > -0.25f) {
                    obstacles.Add(obstacle);
                }
            }

            if (m_scaleViewedPedestrians) {
                for (int i = 0; i < obstacles.Count; i++) {
                    obstacles[i].transform.localScale = Vector3.one * 2f;
                }
            }
            
            return obstacles;
        }

        private List<RVOData> GetPedData() {
            List<RVOData> pd = new List<RVOData>();
            List<RVOEntity> obstacles = GetVisiblePedestrians();
            if (obstacles.Count == 0) return pd;
            foreach(RVOEntity obstacle in obstacles) {
                pd.Add(obstacle.m_rvoData);
            }
            return pd;
        }

        

        [BurstCompile(CompileSynchronously = true)]
        public struct DirectionJob: IJobParallelFor {
            [ReadOnly] public NativeArray<DirData> directions;
            [ReadOnly] public NativeArray<RVOData> pedData;
            [ReadOnly] public int guid;
            [ReadOnly] public float2 pA;
            [ReadOnly] public float2 vA;
            [ReadOnly] public float radius;
            [ReadOnly] public float maxSpeed;
            [ReadOnly] public float aggressiveness;
            [WriteOnly] public NativeArray<DirPenalty> dirPenalties;

            public void Execute(int index) {
                // Get the current direction
                float2 dir = directions[index].direction;

                // Calculate the theta difference from the translated position and the potential direction. This will be used in the loop when checking if a direction is valid.
                float2 potential = (2f*dir)-vA;

                // Iterate through ped data
                float cost = 0f;
                for(int i = 0; i < pedData.Length; i++) {
                    RVOData pd = pedData[i];
                    if (guid == pd.guid) continue;

                    float2 pos = pd.position;
                    float2 vel = pd.velocity;
                    float minkowski_sum = radius + pd.radius;

                    float2 diff_BA = pos - pA;
                    float dist_BA = math.max(math.length(diff_BA), minkowski_sum);
                    float theta_BA = math.atan2(diff_BA[1], diff_BA[0]);
                    float theta_BAort = math.asin(minkowski_sum / dist_BA);
                    float theta_ort_left = theta_BA + theta_BAort;
                    float2 bound_left = new( math.cos(theta_ort_left), math.sin(theta_ort_left) );
                    float theta_ort_right = theta_BA - theta_BAort;
                    float2 bound_right = new( math.cos(theta_ort_right), math.sin(theta_ort_right) );

                    float2 translate_pA = pA + vel;
                    float time_cost = dist_BA / maxSpeed;
                    float theta_right = math.atan2(bound_right[1], bound_right[0]);
                    float theta_left = math.atan2(bound_left[1], bound_left[0]);

                    float2 diff = potential + pA - translate_pA;
                    float theta_diff = math.atan2(diff[1], diff[0]);
                    float angle_diff = theta_diff + 360f;
                    float l = theta_left + 360f;
                    float r = theta_right + 360f;
                    bool isValid;
                    if (r > l)  isValid = (angle_diff >= r || angle_diff <= l);
                    else        isValid = (r <= angle_diff && angle_diff <= l );
                    // Check - are we valid? If not, we break early
                    if (isValid) cost = math.max(cost, (1f+aggressiveness)/time_cost);
                }

                // Save the final cost
                dirPenalties[index] = new DirPenalty {index=index, penalty=directions[index].base_penalty+cost };
            }

        }

        protected void OnDestroy() {
            if (m_jobScheduled) m_dirJobHandle.Complete();
            if (m_directionsArray.IsCreated) m_directionsArray.Dispose();
            if (m_dirPenaltiesArray.IsCreated) m_dirPenaltiesArray.Dispose();
            if (m_pedDataArray.IsCreated) m_pedDataArray.Dispose();
        }

        // Cuz Meta doesn't like OnApplicationQuit
        private void OnApplicationPause() {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_jobScheduled) m_dirJobHandle.Complete();
            if (m_directionsArray.IsCreated) m_directionsArray.Dispose();
            if (m_dirPenaltiesArray.IsCreated) m_dirPenaltiesArray.Dispose();
            if (m_pedDataArray.IsCreated) m_pedDataArray.Dispose();
#endif
        }

        private void OnApplicationQuit() {
            if (m_jobScheduled) m_dirJobHandle.Complete();
            if (m_directionsArray.IsCreated) m_directionsArray.Dispose();
            if (m_dirPenaltiesArray.IsCreated) m_dirPenaltiesArray.Dispose();
            if (m_pedDataArray.IsCreated) m_pedDataArray.Dispose();
        }
    }
}
