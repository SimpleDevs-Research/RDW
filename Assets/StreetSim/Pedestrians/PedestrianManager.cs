using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using AdvancedPeopleSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace StreetSim {
    public class PedestrianManager : MonoBehaviour
    {

        [HideInInspector] public string Description = 
            "This component lies at the top of the Pedestrian Management System of StreetSim. We use this component "
            + "as a singleton manager. It's sole responsibility is to do the following:\n"
            + "\n1. Spawn an initial list of deactivated pedestrians and store references to them in memory, and"
            + "\n2. Constantly shuffle and spawn pedestrian agents whenever they are enabled or disabled.";

        // ==========================================
        // === STATIC ELEMENTS ===
        // ==========================================
        public static PedestrianManager Instance;
        
        // ==========================================
        [Header("=== SPAWN LOGISTICS ===")]
        // ==========================================
        [Tooltip("How many total pedestrians are allowed in the simulation?")] 
        public int numPedestrians = 10;
        [SerializeField, Tooltip("The transform where the spawned agents will be children of")] 
        private Transform _agentParent;
        [SerializeField, Tooltip("The transform that acts as the environment parent root")]
        private Transform _environmentParent;
        [SerializeField, Tooltip("Which demographic preset should we use for determining what kinds of pedestrians are spawned?")] 
        private DemographicsPreset _demographicsPreset;
        [SerializeField, Tooltip("Where will agents be spawned upon initialization in the world? Deactivated agents will reside here")]
        private Vector3 _inactivePos = new Vector3(100, 100, 100);
        [SerializeField, Tooltip("If this Transform is set, it'll save the agents at this transform's position instead and overwrite `InactivePos`")]
        private Transform _inactivePosRef = null;
        

        // ==========================================
        [Header("=== PATHING ===")]
        // ==========================================
        [SerializeField, Tooltip("Define the route nodes that pedestrians can start at.")] 
        private RouteNode[] m_startNodes;
        [SerializeField, Tooltip("Define the route nodes that pedestrians can end at.")] 
        private RouteNode[] m_endNodes;
        [SerializeField, Tooltip("In seconds, what's the min and max amount of possible time to wait between spawns?")] 
        private Vector2 m_spawnDelayMinMax;

        // ==========================================
        [Header("=== EVENTS ===")]
        // ==========================================
        public UnityAction onStarted;
        public UnityAction onPedestrianSpawned;

        // ==========================================
        [Header("=== CACHED DATA - READ-ONLY ===")]
        // ==========================================
        [SerializeField, Tooltip("How many agents have been spawned in total?")]
        private int _totalSpawned = 0;
        // This is a List for all pedestrians, regardless if they're active or not.
        private List<Pedestrian> _allPedestrians = new();
        // This is a List for all ACTIVE pedestrians (i.e. those that are walking around in the scene)
        private List<Pedestrian> _activePedestrians = new();
        // This is a List for all INACTIVE pedestrians (i.e. those available to be spawned again)
        private List<Pedestrian> _inactivePedestrians = new();

        // ==========================================
        // === GETTERS - Read-Only ===
        // ==========================================
        public int numActivePedestrians => _activePedestrians.Count;
        public List<Pedestrian> allPedestrians => _allPedestrians;
        public int totalSpawned => _totalSpawned;
        public Transform environmentParent => _environmentParent;
    

        // =============================================
        // === AWAKE: Instance setting + filling remaining values
        // =============================================
        private void Awake() {
            Instance = this;                        // Singleton logic: Set the current script component as the static Instance
            if (_agentParent == null) {             // Set our agent parent GameObject if not set yet. 
                _agentParent = this.transform;
            }
            if (_environmentParent == null)
            if (_inactivePosRef != null) {          // Set the inactive spawn position to the ref's position, if set
                _inactivePos = _inactivePosRef.localPosition;
            }
        }
        
        // =============================================
        // === START: Spawn initial pedestrians + start custom coroutine loop for spawning
        // =============================================
        private void Start() {
            // We must pre-poo all our pedestrians. We spawn them, and then deactivate them.
            for(int i = 0; i < numPedestrians; i++) {
                // Spawn a new pedestrian. Which pedestrian is likely to be spawned is dependent on the spawn
                // rates defined by the preset for each group.
                Demographic demographic = GetRandomDemographic();
                int index = Mathf.FloorToInt(Random.value * (demographic.pedestrians.Length-1));
                Pedestrian newPed = Instantiate(
                    demographic.pedestrians[index],
                    _inactivePos,
                    Quaternion.identity, 
                    _agentParent
                ) as Pedestrian;
                // We alter some properties about the new pedestrian we spawned
                newPed.gameObject.SetActive(false);
                newPed.gameObject.name = "Pedestrian " + i.ToString();
                newPed.onRouteEnded.AddListener(OnPedestrianReachedEnd);
                // Finally, add it to our list of spawned agents
                _allPedestrians.Add(newPed);
                _inactivePedestrians.Add(newPed);
            }

            // We must now start the coroutine for handling pedestrians
            StartCoroutine(GeneratePedestrians());
            onStarted?.Invoke();
        }

        // =============================================
        // === HELPER: Determine the group to spawn via weighted randomness
        // =============================================
        private Demographic GetRandomDemographic() {
            float randomValue = Random.value;   // between 0 and 1
            float cumulativeWeight = 0f;
            foreach(DemographicSpawning group in _demographicsPreset.demographics) {
                cumulativeWeight += group.spawnWeight;
                if (randomValue <= cumulativeWeight) {
                    return group.demographic;
                }
            }
            // Fallback for floating-point precision issues
            return _demographicsPreset.demographics[0].demographic;
        }

        // =============================================
        // === CUSTOM UPDATE LOOP: Spawning pedestrians when possible
        // =============================================
        private IEnumerator GeneratePedestrians() {
            // We use a `while` loop to keep it running
            while (true) {

                // Can't dp anything if there are no inactive pedestrians that can be spawned.
                if (_inactivePedestrians.Count == 0) {
                    yield return null;
                    continue;
                }

                // We need to initialize the new pedestrian now. For now, we just grab the first inactive pedestrian
                Pedestrian newPed = _inactivePedestrians[0];
                // If this new pedestrian is already active in the hierarchy for some reason, we must skip.
                if (newPed.gameObject.activeInHierarchy) {
                    yield return new WaitForSeconds(Random.Range(m_spawnDelayMinMax.x, m_spawnDelayMinMax.y));
                    continue;
                }

                // We need to be able to pick where the agent is spawned, and where their destination is located.
                // To do this, we use pure randomization to determine the of start and end RouteNodes. Note that we also make sure
                // that the start and end indices are not the same.
                int startIndex = Random.Range(0, (int)m_startNodes.Length);
                int endIndex = Random.Range(0, (int)m_endNodes.Length);
                while (m_startNodes[startIndex] == m_endNodes[endIndex]) {
                    endIndex = Random.Range(0, (int)m_endNodes.Length);
                }
                RouteNode startNode = m_startNodes[startIndex];
                RouteNode endNode = m_endNodes[endIndex];

                // The start and end position of the Pedestrian itself are random points in the start and end nodes
                // Note tht these are in world positions
                Vector3 startPos = startNode.GetRandomHorizontalPosition();
                Vector3 endPos = endNode.GetRandomHorizontalPosition();

                // When getting the route from `RouteManager`, we can use `startNode` and `endNode`.
                // Then, we check: are our start and end positions located within the first and last nodes? 
                // If so, we cull them out. This leaves the route consisting of a sequence of nodes between our start and end
                List<RouteNode> route = RouteManager.Instance.GetRoute(startNode, endNode, newPed.personality);
                if (route.Count > 0 && route[0].CheckWithinRadius(startPos)) route.RemoveAt(0);
                if (route.Count > 0 && route[route.Count-1].CheckWithinRadius(endPos)) route.RemoveAt(route.Count-1);

                // Now we can set those values!
                newPed.SetRoute(startPos, endPos, route);
                newPed.SetIntention(Pedestrian.Intention.TRAVEL);

                // Now, we initialize the agent's starting position and rotation
                newPed.transform.position = startPos;
                newPed.transform.rotation = startNode.transform.rotation;  

                // We also initialize the agent's RVO state
                newPed.GetComponent<PedestrianRVO>().RVOActive = true;

                // In closing, we now make sure to add this agent to our list of active pedestrians, set it to active, and upate our spawn count
                _inactivePedestrians.Remove(newPed);  
                _activePedestrians.Add(newPed);
                newPed.gameObject.SetActive(true);
                _totalSpawned++;

                // Call events
                onPedestrianSpawned?.Invoke();

                // Finally, we wait a random amount 
                yield return new WaitForSeconds(Random.Range(m_spawnDelayMinMax.x, m_spawnDelayMinMax.y));
            }
        }

        // =============================================
        // === PEDESTRIAN DEACTIVATION EVENT ===
        // =============================================
        public void OnPedestrianReachedEnd(Pedestrian p) {
            // Check: is it one of our own pedestrians? If so, ignore.
            if (!_allPedestrians.Contains(p)) return;

            // Deactivate the pedestrian and move it to the inactive location
            p.gameObject.SetActive(false);
            p.transform.localPosition = _inactivePos;

            // Migrate from active to inactive
            _activePedestrians.Remove(p);
            _inactivePedestrians.Add(p);
        }

        // =============================================
        // === ON DESTROY ===
        // Disconnect listener `OnPedestrianReachedEnd` from other pedestrians
        // =============================================
        private void OnDestroy() {
            foreach(Pedestrian p in _allPedestrians) {
                p.onRouteEnded.RemoveListener(OnPedestrianReachedEnd);
            }
        }

    }
}
