using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RVO;

/*
This is an extension of the very efficient `Generator` class in 
the "RVO" package I wrote up. It contains a TON of functions, but 
we need to overwrite it to make it align with StreetSim

Some of these changes include:
- Making the agent spawning time-based, compatible with Demographics, and at random positions
- Modifying agents' destinations to encompass global pathing
*/

namespace StreetSim {
    public class PedestrianGenerator : RVO.Generator
    {
        
        // The base `Generator` script already gives us a TON of useful parameters we can use.
        // These ones below are unique to StreetSim.

        [Header("=== Street Sim Pedestrians Setup ===")]
        // Agents' initial spawn point. Deactivated agents will also reside here. 
        // If you set `_inactivePosRef` instead, that'll overwite this
        public Vector3 inactivePos = new Vector3(-100, -100, -100);         
        public Transform inactivePosRef = null;
        public RouteNode[] startNodes;                  // The route nodes that pedestrians can start at. 
        public RouteNode[] endNodes;                    // The route nodes that pedestrians can end at.
        public Vector2 spawnDelayMinMax;                // In seconds, the min and max time to wait between spawns.

        [Header("=== Optimizations ===")]
        //public bool hideAnimationOnStart = false;       // The `Pedestrian` class has a `ToggleAnimation(bool)`. Toggle this to initialize animation on start.
        public bool cullByDistance = true;
        public float maxCullDistance = 25f;
        private CullingGroup _cullingGroup;
        private BoundingSphere[] _cullingBounds;

        [Header("=== CACHED DATA - READ-ONLY ===")]
        [SerializeField] private int _totalSpawned = 0;         // How many agents have been spawned in total?
        [SerializeField] private int _totalActive = 0;
        private List<Pedestrian> _allPedestrians = new();      // This is a List for all pedestrians
        private List<Pedestrian> _activePedestrians = new();   // This is a List for all ACTIVE pedestrians
        private List<Pedestrian> _inactivePedestrians = new();  // This is a List for all INACTIVE pedestrians
        private Coroutine spawnCoroutine = null;


        // `Generator` has an `Awake()` function already that does a ton of prep. This `Awake` calls a `Generate` 
        // function that generates all agents before the first frame. There's nothing wrong with spawning
        // all pedestrians at the beginning, so we won't overwrite this part... at least, in principle.

        // The one change we have to do though is ensure that `record_data` is UNCHECKED.
        // This prevents the base `Generator` script from writing stuff down first.
        // Instead, we'll force it to call in `Start()`. We'll use `OnValidate()` to
        // ensure that it stays unchecked no matter what. We'll expose a new variable, `record_pedestrians`, 
        // that fulfills the role instead in `Start()`.
        
        [Header("=== Recording Pedestrians Data ===")]
        public bool record_pedestrians = true;

        // In `Start()`, we give RouteManager time to process all nodes first

        protected void Start() {
            //hideAnimationOnStart = Object.FindAnyObjectByType<PedestrianOccluder>() != null;
            if (cullByDistance) InitializeCullingSystem();
            // Initialize recorder
            if (record_pedestrians) recorder.StartRecording(this);
            // Invoke the coroutine to initialize the loop to activate robots over time
            spawnCoroutine = StartCoroutine(SpawnCoroutine());
        }

        private void InitializeCullingSystem() {
            // Initialize culling group
            _cullingGroup = new CullingGroup();
            _cullingGroup.targetCamera = RDW.Player.Instance.centerEyeCamera.GetComponent<Camera>();
            _cullingGroup.SetBoundingDistances(new float[] { maxCullDistance });

            // Initialize bounds array
            _cullingBounds = new BoundingSphere[num_agents];

            // Cache all data upfront
            for (int i = 0; i < num_agents; i++) {
                Pedestrian pedestrian = _allPedestrians[i];
                _cullingBounds[i] = new BoundingSphere(pedestrian.transform.position, 1f);
            }

            // Update culling group
            _cullingGroup.SetBoundingSpheres(_cullingBounds);
            _cullingGroup.SetBoundingSphereCount(num_agents);
            _cullingGroup.onStateChanged = OnCullingStateChanged;
        }

        private void OnCullingStateChanged(CullingGroupEvent sphereEvent) {
            // Direct array lookup using the index provided by the CullingGroup API
            bool isInsideRange = sphereEvent.currentDistance == 0;
            Pedestrian p = _allPedestrians[sphereEvent.index];
            p.ToggleAnimation(isInsideRange);
        }

        // The function we definitely need to modify is `GenerateAgent(i)`. This is a function that is
        // called in `Generate()` in a `for` loop.

        protected override void GenerateAgent(int agent_index) {
            // Step 1a. Generate the start and end positions of the agent. However,
            // As this is currently an INACTIVE agent, the start and end must be the same.
            Vector3 pos = inactivePos;
            Vector3 dest = inactivePos;

            // Step 1b. Calculate additional properties based on the start and end positions.
            // Note that we need to determine the personality for this agent. This is done via 
            // `demographic.GetRandomPersonality()`. It's not truly random - it takes into account 
            // the ratios defined for each demographic, for each personality type. In the latest 
            // version of Unity-RVO, each Personality also contains a reference to an agent prefab
            // tied to that personality! Keep in mind that since the agent is inactive,
            // MAKE SURE TO MAKE THE PREFAB NOT ACTIVE IN HIERARCHY BY DEFAULT
            Vector3 forward = Vector3.forward;
            Personality p = demographics.GetRandomPersonality();
            GameObject personality_agent_prefab = p.GetRandomAgent();

            // Step 2: Instantiate the agent itself. This is just lifted from the original function.
            GameObject go = Instantiate(personality_agent_prefab, pos, Quaternion.LookRotation(forward));
            Transform t = go.transform;
            t.parent = agent_parent;

            // Step 3: Inform our agent data in vo_op. Again, this is lifted from the original
            vo_op.AddAgent(agent_index, pos, dest, t, p);

            // Step 4: Miscellaneous. For Robot components and KDTree stuff. Also lifted from the original.
            agent_positions[agent_index] = pos;
            Robot ad = go.GetComponent<Robot>();
            if (ad != null) {
                ad.agent_index = agent_index;
                ad.generator = this;
                ad.personality = p;
            }

            // Step 5: Add our agent into both `_allPedestrians` and `_inactivePedestrians` if 
            // this agent has a `Pedestrian` component
            Pedestrian pedestrian = go.GetComponent<Pedestrian>();
            if (pedestrian != null) {
                _allPedestrians.Add(pedestrian);
                _inactivePedestrians.Add(pedestrian);
            }
        }

        // One thing you should really understand: VO/RVO/HRVO only should affect CPU transforms, but not in reverse!
        // In other words, if there's any situation where we do things like re-position or translate using Transforms,
        // they won't necessarily be reflected in JOB code. This can create a mismatch between CPU and Job Burst array 
        // data, or even overwrite the Transform (because a part of the Job Burst system is to set things like )

        // ============================================
        // NOTE: HOW THIS SCRIPT OPERATES (lifted from the original `Generator` script)
        // This script encompasses 3 distinct levels of a simulation: 
        // 1. OBSERVATION: Agents identify who their closest neighbors are
        // 2. PROCESSING: Agents will determine optimal velocities to move towards based on RVO
        // 3. MOVEMENT: Agents will adjust their positions and current velocities to reflect Step 2.
        // Because this is a base class, we assume that Steps 1 and 2 will be conducted in the Update loop, while Step 3 is done in a LateUpdate loop
        // We provide the base classes for Observation, Processing, and Movement as well.
        // If you want to modify any of these operations, you can create your own inherited child of this script and modify them.
        // ============================================

        // In this version, we basically don't want to mess with the order of operations necessarily.
        // We have 3 options to modify: 
        // -------------------------------------------------
        //  protected virtual void Update() {
        //      Observation();                  // Vision
        //      Processing(Time.deltaTime);     // Local Collision Avoidance
        //      Movement(Time.deltaTime);       // Movement
        //  }
        // -------------------------------------------------
        // Here are some notes on each:
        //  1. Observation  <-  Tracks which agents are our neighbors and if we're colliding with any. 
        //                      Handles inactive agents. Pretty robust.
        //  2. Processing   <-  This one REALLY just calls `vo_op.Execute(deltaTime)`, so we'll take a 
        //                      look at that. Nothing else here
        //  3. Movement     <-  This one executes a parallel job to actually translate the agents via 
        //                      local position. We shouldn't really mess with this necessarily...
        // Honestly, there's not a lot we can really change here. The only thing that's missing is the 
        // integration of route management and pathfinding.
        //
        // There's one more unique function provided:
        // -------------------------------------------------
        //  // Helper: if we want to toggle specific agents or not, do so here
        //  public virtual void ToggleRobot(int agent_index, bool to_toggle) {
        //      vo_op.transforms[agent_index].gameObject.SetActive(to_toggle);
        //      vo_op.active[agent_index] = to_toggle;
        //  }
        // -------------------------------------------------
        // This is a helper function to toggle specific agents. This does two things simultaneously:
        // 1. Sets the gameobject itself as inactive, and
        // 2. Deactivates the agent in `vo_op`'s boolean NativeArray. 
        // This gives us a hint as to how to incorporate the logic of disabling pedestrians.

        // The idea here is now this:
        //  1.  We can create a `Pedestrian` component that extends `Robot`. Right now, `Robot` just does Gizmos stuff
        //  2.  Inside of `Pedestrian`, we can do things like query for new routes, set current destinations, and toggle 
        //      agents off when they reach their destinations.
        //  3. We can either expand `ToggleRobot` or create another custom function that invokes it.
        //  4. This component needs to enable deactivated robots/pedestrians in sequential order, in a way that makes sense.

        private IEnumerator SpawnCoroutine() {
            // This keeps this a continuous loop
            while(true) {
                // We skip if our list of inactive agents is empty
                if (_inactivePedestrians.Count == 0) {
                    yield return null;
                    continue;
                }

                // We need to initialize the new pedestrian now. For now, we just grab the first inactive pedestrian
                Pedestrian pedestrian = _inactivePedestrians[0];
                _inactivePedestrians.RemoveAt(0);

                // If this new pedestrian is already active in the hierarchy for some reason, we must skip.
                if (pedestrian.gameObject.activeInHierarchy) {
                    yield return new WaitForSeconds(Random.Range(spawnDelayMinMax.x, spawnDelayMinMax.y));
                    continue;
                }

                // We need to be able to pick where the agent is spawned, and where their destination is located.
                // To do this, we use pure randomization to determine the of start and end RouteNodes. Note that we also make sure
                // that the start and end indices are not the same.
                int startIndex = Random.Range(0, (int)startNodes.Length);
                int endIndex = Random.Range(0, (int)endNodes.Length);
                while (startNodes[startIndex] == endNodes[endIndex]) {
                    endIndex = Random.Range(0, (int)endNodes.Length);
                }
                RouteNode startNode = startNodes[startIndex];
                RouteNode endNode = endNodes[endIndex];

                // The start and end position of the Pedestrian itself are random points in the start and end nodes
                // Note tht these are in world positions
                Vector3 startPos = startNode.GetRandomHorizontalPosition();
                Vector3 endPos = endNode.GetRandomHorizontalPosition();

                // When getting the route from `RouteManager`, we can use `startNode` and `endNode`.
                // Then, we check: are our start and end positions located within the first and last nodes? 
                // If so, we cull them out. This leaves the route consisting of a sequence of nodes between our start and end
                List<RouteNode> route = RouteManager.Instance.GetRoute(startNode, endNode, pedestrian.personality);
                if (route.Count > 0) route.RemoveAt(0);
                if (route.Count > 0) route.RemoveAt(route.Count-1);

                // Now we have to set those values.
                // The issue here is that there's two separate threads: the Job Burst system, which doesn't get updated by 
                // changes in Transform in the CPU end, and the ... CPU end of things (technically both are in the CPU but
                // you get what I mean). To change things, we need to change both.

                // Pedestrian comes with `SetRoute`, which updates the pedestrians' route cache on the CPU. That doesn't have to 
                // be stored in the Jobs system. However, what needs to be changed is the position, rotation, and destination
                // of the pedestrian in the Jobs system.
                // Pedestrian.SetRoute(start, end, route) 
                //      -> Pedestrian stores the route data 
                //      -> Pedestrian calculates current destination and updates it in VO (Pedestrian.UpdateCurrentDestination())
                // Pedestrian.UpdatePose(start, rotation)
                pedestrian.UpdatePose(startPos, startNode.rotation); 
                pedestrian.SetRoute(startPos, endPos, route);
                //pedestrian.ToggleAnimation(!hideAnimationOnStart);
                pedestrian.ToggleAnimation(!cullByDistance);
                vo_op.reached_destination[pedestrian.agent_index] = false;

                // In closing, we now make sure to set it to active and update our spawn count
                ToggleRobot(pedestrian.agent_index, true);
                _activePedestrians.Add(pedestrian);
                _totalSpawned++;
                yield return new WaitForSeconds(Random.Range(spawnDelayMinMax.x, spawnDelayMinMax.y));
            }
        }


        // There's also a `LateUpdate()` whose primary purpose is to update the component's KDTree.
        // What?! There's a KDTree? Yes, there is. This KDTree is key for `Observation()`, because
        // Agents use it for their nearest neighbor detection.
        //
        // We want to take advantage of this too. In this scenario, we want to make sure that agents
        // move along not just to their current destination, but also their trajectory. So we need to check:
        // 1. Which agents reached their destination?
        // 2. For those who have, double-check if they can set another current destination or if they truly reached the end
        // 3. For those who have reached the end, move them to inactive state
        // 4. For those who haven't reached the end, update their current destination

        protected override void LateUpdate() {
            // new check: who's reached their destinations?
            Queue<Pedestrian> toCheck = new Queue<Pedestrian>(_activePedestrians); 
            while (toCheck.Count > 0) {
                Pedestrian pedestrian = toCheck.Dequeue();
                //  If culling, update their bounds
                if (cullByDistance) {
                    _cullingBounds[pedestrian.agent_index].position = pedestrian.transform.position;
                }
                // Check if pedestrian has reached the end.
                if (!pedestrian.ValidateRoute()) {
                    // This pedestrian has truly reached the end.
                    ToggleRobot(pedestrian.agent_index, false);
                    _activePedestrians.Remove(pedestrian);
                    _inactivePedestrians.Add(pedestrian);
                    continue;
                }
                // If still active, then we update their animators
                pedestrian.UpdateAnimator();
            }

            // We can now update KDTree
            base.LateUpdate();
            _totalActive = _activePedestrians.Count;
        }


        private void OnValidate() {
            record_data = false;    // force data recording in awake to initialize. We'll use `record_pedestrians` for this.
            if (inactivePosRef != null) inactivePos = inactivePosRef.position;
        }

        protected override void OnDisable() {
            base.OnDisable();
            if (_cullingGroup != null) {
                _cullingGroup.Dispose();
                _cullingGroup = null;
            }
        }

    }
}