using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;
using Random = UnityEngine.Random;

namespace RVO {

    [System.Serializable]
    public class RVO_Robot {
        public int index;
        public Vector2 position;
        public Vector2 velocity;
        public float maxSpeed;
        public float radius;
        public float aggressiveness;
        public Color color;
        public Vector2 radiusRange;
        public bool gizmos = true;

        [Header("------")]
        private bool instantiated = false;
        public bool active;
        [SerializeField] private int numIterations; 
        [SerializeField] private int numNeighbors;
        [SerializeField] private Vector2 destination;
        public Vector3 destination3D => new Vector3(destination.x, 0f, destination.y);
        [SerializeField] private Vector2 desiredVelocity;
        public Vector3 desiredVelocity3D => new Vector3(desiredVelocity.x, 0f, desiredVelocity.y);
        [SerializeField] private Vector2 optimalVelocity;
        public Vector3 optimalVelocity3D => new Vector3(optimalVelocity.x, 0f, optimalVelocity.y);
        [SerializeField] private bool hasToRedirect;
        [HideInInspector] public List<RVO_BA> RVO_BA_ALL; 
        [HideInInspector] public List<Vector2> dirs;
        public Transform transformRef = null;
        public Transform destinationRef = null;
        public int gridCellIndex = -1;

        public Vector3 position3D => new Vector3(this.position.x, 0f, this.position.y);
        public Vector3 velocity3D => new Vector3(this.velocity.x, 0f, this.velocity.y);
        public RVO_Robot(int index, Vector2 position, Vector2 velocity, float maxSpeed, float radius, Vector2 radiusRange, float aggressiveness, Color color) {
            this.index = index;
            this.position = position;
            this.velocity = velocity;
            this.maxSpeed = maxSpeed;
            this.radius = radius;
            this.radiusRange = radiusRange;
            this.aggressiveness = aggressiveness;
            this.color = color;
            this.destination = this.position;
            this.hasToRedirect = false;
            this.RVO_BA_ALL = new List<RVO_BA>();
            this.active = true;
            this.numIterations = 0;
            this.instantiated = true;
        }
        public RVO_Robot(int index, Vector3 position, Vector3 velocity, float maxSpeed, float radius, Vector2 radiusRange, float aggressiveness, Color color) {
            this.index = index;
            this.position = new Vector2(position.x, position.z);
            this.velocity = new Vector2(velocity.x, velocity.z);
            this.maxSpeed = maxSpeed;
            this.radius = radius;
            this.radiusRange = radiusRange;
            this.aggressiveness = aggressiveness;
            this.color = color;
            this.destination = this.position;
            this.hasToRedirect = false;
            this.RVO_BA_ALL = new List<RVO_BA>();
            this.active = true;
            this.numIterations = 0;
            this.instantiated = true;
        }

        public void Initialize(int index, Vector3 position, Vector3 velocity, float maxSpeed, float radius, Vector2 radiusRange,float aggressiveness, Color color) {
            this.index = index;
            this.position = (this.transformRef != null) 
                ? new Vector2(this.transformRef.position.x, this.transformRef.position.z)
                : new Vector2(position.x, position.z);
            this.velocity = new Vector2(velocity.x, velocity.z);
            this.maxSpeed = maxSpeed;
            this.radius = radius;
            this.radiusRange = radiusRange;
            this.aggressiveness = aggressiveness;
            this.color = color;
            this.destination = this.position;
            this.hasToRedirect = false;
            this.RVO_BA_ALL = new List<RVO_BA>();
            this.active = true;
            this.numIterations = 0;
            this.instantiated = true;
        }

        public void SetDestination(Vector2 target) {
            this.destination = target;
        }
        public void SetDestination(Vector3 target) {
            this.destination = new Vector2(target.x, target.z);
        }
        public void SetDestinationRef(Transform d) {
            this.destinationRef = d;
            this.SetDestination(d.position);
        }

        public void GetDesiredVelocity() {
            // If the destinationRef is not null, we have to update the destination
            if (this.destinationRef != null) this.SetDestination(this.destinationRef.position);
            // The desired velocity is essentially a direct vector towards our current target
            Vector2 diff = this.destination - this.position;
            // Depending on how far away we are from the target, we either set to the desired direction capped to maxSpeed
            if (Vector2.Distance(this.position, this.destination) > 0.1f) {
                this.desiredVelocity = diff.normalized * Mathf.Clamp(diff.magnitude, 0f, this.maxSpeed);
                return;
            }
            // ... or we set to 0 because we don't need to move anymore
            this.desiredVelocity = Vector2.zero;
        }

        public void UpdateRobot(RVO_Robot[] robots, Vector2[] directions, float deltaTime, bool locomote = true) {
            if (!this.instantiated) return;
            this.numNeighbors = robots.Length;
            // If this is attached to a GameObject, we make sure we are in sync.
            if (this.transformRef != null) {
                this.position = new Vector2(
                    this.transformRef.position.x,
                    this.transformRef.position.z
                );
            }
            if (this.numIterations % 3 == 0) {
                // Get the desired velocity (capped to maxSpeed)
                GetDesiredVelocity();
                // We gotta do the RVO
                GetRVO(robots, directions, deltaTime);
            }
            // If we gotta locomote, locomote
            if (locomote) MoveRobot(deltaTime);
            // Update numiterations
            this.numIterations += 1;
            // Adjust our radius based on a min-max radius field and current velocity
            float radiusRatio = this.velocity.magnitude / this.maxSpeed;
            this.radius = (1f - radiusRatio) * this.radiusRange.x + radiusRatio * this.radiusRange.y;
            // Update our gameobject position
            if (this.transformRef != null) {
                this.transformRef.position = this.position3D;
            }
        }

        private void GetRVO(RVO_Robot[] robots, Vector2[] directions, float deltaTime) {
            // Re-adjust the radius
            float ROB_RAD = this.radius;
            // Calculate the Minkowski sum beforehand
            float MINKOWSKI_RAD = 2f * ROB_RAD;
            // Generate list of all RVO regions
            this.RVO_BA_ALL = new List<RVO_BA>();
            // Right now, the optimal velocity is the desired velocity. 
            // This is so that if there are no other robots, we simply doot-doot to our desired velocity
            Vector2 optimalVelocity = this.desiredVelocity;
            // Copy the list of directions
            List<Vector2> suitableDirections = new List<Vector2>(directions);
            // Generate list of penalties
            float[] timeCost = new float[robots.Length];
            int[] directionRobotMap = new int[directions.Length];
            // Sort this list in order of closest to our desired velocity
            suitableDirections.Sort((v1,v2)=>(Vector2.Dot(this.desiredVelocity,v2)).CompareTo(Vector2.Dot(this.desiredVelocity,v1)));
            // With that out of the way, we can begin to compare between this robot and all other robots
            // Loop through all robots
            Vector2 averageVel = Vector2.zero;
            Vector2 pB, vB, diff_BA, diff_BA_norm;
            float dist_BA;
            bool desiredIsSuitable = true;
            int n = 0;
            for(int i = 0; i < robots.Length; i++) {
                // skip if we're meeting ourselves
                if (robots[i].index == this.index) continue;

                // Copy suitableDirections into a temp one
                List<Vector2> tempSuitable = new List<Vector2>(suitableDirections);

                pB = robots[i].position;        // position of the other robot
                vB = robots[i].velocity;        // velocity of the other robot
                
                // Calculate the translation from pA to the new "triangle space"
                Vector2 translate_pA = this.position + vB;                // VO
                //Vector2 translate_pA = this.position + 0.5f*(vB + this.velocity);      // RVO
                
                // Calculate the left and right bounds of the Minkowski Sum
                // Step 1: Get the distance between the two positions and the angle between the two of them, relative to ----> axis
                dist_BA = Vector2.Distance(this.position, pB);
                diff_BA = pB - this.position;
                diff_BA_norm = diff_BA.normalized;
                float theta_BA = Mathf.Atan2(diff_BA.y, diff_BA.x);
                // Step 2: Restrict the lower bound of the distance between the two to that of 2x the robot radius
                if (MINKOWSKI_RAD > dist_BA) dist_BA = MINKOWSKI_RAD;
                // Step 3: Get the angle between the direct vector towards B and the outer left and right vectors that are tangential
                float theta_BAort = Mathf.Asin(MINKOWSKI_RAD / dist_BA);
                // Step 4: Get the left and right tangential vectors to B that represent the pyramid from A to B's sides
                float theta_ort_left = theta_BA + theta_BAort;
                Vector2 bound_left = new Vector2(
                    Mathf.Cos(theta_ort_left), 
                    Mathf.Sin(theta_ort_left)
                );
                float theta_ort_right = theta_BA - theta_BAort;
                Vector2 bound_right = new Vector2(
                    Mathf.Cos(theta_ort_right), 
                    Mathf.Sin(theta_ort_right)
                );

                // Calculate the time cost for this robot
                timeCost[i] = dist_BA / this.maxSpeed;

                // Right now, we have the left and right bounds, as well as the M.Sum relative to pB.
                // We need to check 2 things:
                // 1) Get the dot product between the diff_BA and one of the bounds
                // 2) if we look at our current desired velocity, will it be in RVO?
                // 3) For all vertices in suitableVelocities, will they be in RVO?
                // 4) Calculate the average velocity of all nearby robots

                float theta_right = Mathf.Atan2(bound_right.y, bound_right.x);
                float theta_left = Mathf.Atan2(bound_left.y, bound_left.x);

                // Check if the desired velocity is in (R)VO
                //if (VelInVO(translate_pA, this.position, theta_left, theta_right, this.desiredVelocity)) {
                if (VelInVO(translate_pA, this.position, theta_left, theta_right, 2f*this.desiredVelocity-this.velocity)) {
                    desiredIsSuitable = false;
                }
                // For all suitable vectors, check if they'll be in the (R)VO
                for(int j = 0; j < suitableDirections.Count; j++) {
                    // Get the velocity
                    Vector2 potential = suitableDirections[j];
                    Vector2 potentialCapped = potential.normalized * Mathf.Clamp(potential.magnitude, 0f, this.maxSpeed);
                    //if (VelInVO(translate_pA, this.position, theta_left, theta_right, potentialCapped)) {
                    if (VelInVO(translate_pA, this.position, theta_left, theta_right, 2f*potentialCapped-this.velocity)) {
                        // In this case, this is not a suitable velocity. Kill it off while we can
                        int dirIndex = System.Array.IndexOf(directions, potential);
                        directionRobotMap[dirIndex] = i;
                        tempSuitable.Remove(potential);
                    }
                }

                /*
                // 1) Dot prod
                //?/// float boundDot = Vector2.Dot(diff_BA_norm, bound_left.normalized);
                // 2) Check if desiredVelocity is in RVO
                ////// if (VelInRVO(this.position, this.velocity, pB, vB, MINKOWSKI_RAD, this.desiredVelocity, deltaTime)) {
                //if (VelInRVO(this.position, this.velocity, diff_BA_norm, boundDot, this.desiredVelocity)) { 
                    // Unfortunately, our desired vel won't cut it. So we have to skip it
                /////    desiredIsSuitable = false;
                /////}
                // 3) For all vectors in suitableVelocities, will they be in RVO?
                foreach(Vector2 potential in suitableDirections) {
                    Vector2 potentialCapped = potential.normalized * Mathf.Clamp(potential.magnitude, 0f, this.maxSpeed);
                    if (VelInRVO(this.position, this.velocity, pB, vB, MINKOWSKI_RAD, potentialCapped, deltaTime)) {
                    //if (VelInRVO(this.position, this.velocity, diff_BA_norm, boundDot, potentialCapped)) {
                        // In this case, this is not a suitable velocity. Kill it off while we can
                        tempSuitable.Remove(potential);
                    }
                }
                */

                // Save the remaining suitable directions
                suitableDirections = tempSuitable;
                // 4) Calculate average vel of all nearby robots
                averageVel += vB;
                n += 1;

                // Might as well add bound data to rvo_all
                // RVO
                //Vector2 transl_vB_vA = this.position + 0.5f * (vB + this.velocity);
                this.RVO_BA_ALL.Add(new RVO_BA(translate_pA, bound_left, bound_right, pB, vB, dist_BA, MINKOWSKI_RAD));
                /*
                
                // VO
                // Vector2 transl_vB_vA = pA + vB
                // HRVO
                //float dist_dif = Vector2.Distance(0.5f * (vB - this.velocity), Vector2.zero);
                //transl_vB_vA = new Vector2(
                //    this.position.x + vB.x + Mathf.Cos(theta_ort_left), 
                //    this.position.y + vB.y + Mathf.Sin(theta_ort_left)
                //) * dist_dif;
                // Save the results for processing
                this.RVO_BA_ALL.Add(new RVO_BA(transl_vB_vA, bound_left, bound_right, pB, vB, dist_BA, 2*ROB_RAD));
                */
            }
            // Now with all other objects processed, we can determine an "optimal" velocity
            //this.optimalVelocity = this.GetOptimalVelocity(this.desiredVelocity, this.RVO_BA_ALL, directions, out this.hasToRedirect);

            // AT this point, if our desired is suitable, then just go with it
            this.dirs = suitableDirections;
            if (desiredIsSuitable) {
                this.optimalVelocity = this.desiredVelocity;
                return;
            }
            // If not desirable, we get the closest in suitableDirections
            if (suitableDirections.Count > 0) {
                this.optimalVelocity = suitableDirections[0].normalized * Mathf.Clamp(suitableDirections[0].magnitude, 0f, this.maxSpeed);
                return;
            }
            // If no other options, we are out of options. We must determine a new velocity that is penalized.
            Vector2 bestVelocityWithPenalty = directions[0].normalized * this.maxSpeed;
            float bestPenalty = GetPenalty(this.desiredVelocity, bestVelocityWithPenalty, timeCost[directionRobotMap[0]], this.aggressiveness);
            for(int j = 1; j < directions.Length; j++) {
                // Get the capped vel
                Vector2 tempDirVel = directions[j].normalized * this.maxSpeed;
                float tempPenalty = GetPenalty(this.desiredVelocity, tempDirVel, timeCost[directionRobotMap[j]], this.aggressiveness);
                if (tempPenalty < bestPenalty) {
                    bestVelocityWithPenalty = tempDirVel;
                    bestPenalty = tempPenalty;
                }
            }
            this.optimalVelocity = bestVelocityWithPenalty;
        }

        private static bool VelInVO(Vector2 p0, Vector2 pA, float theta_left, float theta_right, Vector2 vQuery) {
            // Get a new velocity based on the translation
            Vector2 dif = vQuery + pA - p0;
            // Calculate the angles involved, all relative to the + x-axis
            float theta_dif = Mathf.Atan2(dif.y, dif.x);
            // Check if the angle of dif is between the angle of theta_right and theta_left
            return InBetween(theta_dif, theta_left, theta_right);
        }

        private static bool InBetween(float theta_dif, float theta_left, float theta_right) {
            // There is a profound issue: we can have negative thetas.
            // Firstly, add all vectors by 360
            float dif = theta_dif + 360f;
            float l = theta_left + 360f;
            float r = theta_right + 360f;
            // Second, check if R > L or L < R
            if (r > l) {
                // This produces an OR condition: either dif >= r or dif <= l
                return (dif >= r || dif <= l);
            } 
            // This produces an AND condition: r <= dif <= l
            return (r <= dif && dif <= l );
            /*
            // Before outright comparing using a 2-sided inequality, we need to check two scenarios: 
            //  That the left bound is south-facing and the right bound is north-facing (forming < kind of bound)
            if (theta_left < 0f && theta_right > 0f) {
                // We extend the angle from < 0 degrees to something that's between 0 and 360
                float new_theta_left = theta_left + 2f * Mathf.PI;
                // We do the same with the diff angle, if it happens to be < 0 degrees
                float new_theta_dif = (theta_dif < 0f) ? theta_dif + 2f*Mathf.PI : theta_dif;
                // Now we can do the check
                return (theta_right <= new_theta_dif && new_theta_dif <= new_theta_left);
            }
            // Otherwise, we can just do the check like normal
            return (theta_right <= theta_dif && theta_dif <= theta_left); 
            */
        }

        private static float GetPenalty(Vector2 vDesired, Vector2 vQuery, float timeCost, float aggressiveness = 1f) {
            return aggressiveness/timeCost + (vDesired - vQuery).magnitude;
        }

        private static bool VelInRVO(Vector2 pA, Vector2 vA, Vector2 pB, Vector2 vB, float rad, Vector2 queryV, float deltaTime) {
            // First, we need to get the velocity vector to compare with
            Vector2 vPrime = 2f*queryV - vA;
            // Second, we need to get the new position based on vPrime
            Vector2 newPos = pA + deltaTime * (vPrime - vB);
            // Third, we check if the new position is inside the M.Sum relative to pB
            return (pB - newPos).magnitude <= rad;
        }

        
        private static bool VelInRVO(Vector2 pA, Vector2 vA, Vector2 diff_BA_norm, float boundDot, Vector2 queryV) {
            // First, we need to get the velocity vector to compare with
            Vector2 vPrime = 2f * queryV - vA;
            // Second, get the dot product between vPrime and the vector from A to B
            float d = Vector2.Dot(vPrime.normalized, diff_BA_norm);
            return d >= boundDot;
        }
        

        private void MoveRobot(float t) {
            if (Vector2.Distance(this.position, this.destination) < 0.1f) {
                this.velocity = Vector2.zero;
                this.position = this.destination;
                this.active = false;
                return;
            }

            this.velocity = this.optimalVelocity;
            // Rotate the velocuty
            //Vector3 intendedVel = Vector3.RotateTowards(this.velocity3D, this.desiredVelocity3D, 10f*t, 0f); 
            //this.velocity = new Vector2(intendedVel.x, intendedVel.z);
            //this.velocity = this.velocity.normalized * Mathf.Clamp(this.velocity.magnitude, 0f, 0.25f);
            this.position += this.velocity * t;
            
        }
    }
    
    [System.Serializable]
    public class RVO_BA {
        public Vector2 transl_vB_vA;
        public Vector2 bound_left, bound_right;
        public Vector2 vB;
        public Vector2 pB;
        public float dist_BA;
        public float rad;
        public RVO_BA(Vector2 transl_vB_vA, Vector2 bound_left, Vector2 bound_right, Vector2 pB, Vector2 vB, float dist_BA, float rad) {
            this.transl_vB_vA = transl_vB_vA;
            this.bound_left = bound_left;
            this.bound_right = bound_right;
            this.pB = pB;
            this.vB = vB;
            this.dist_BA = dist_BA;
            this.rad = rad;
        }
    }

    [System.Serializable]
    public class RVOProfile {
        public float radius;
        public Vector2 radiusRange;
        public float maxSpeed;
        public Color color;

        public RVOProfile(float radius, Vector2 radiusRange, float maxSpeed, Color color) {
            this.radius = radius;
            this.radiusRange =radiusRange;
            this.maxSpeed = maxSpeed;
            this.color = color;
        }

        public RVOProfile clone() {
            return new RVOProfile(this.radius, this.radiusRange, this.maxSpeed, this.color);
        }
    }

    public class Grid {
        public static Vector2Int GetGridXY(Vector3 pos, Vector3 gridCenter, Vector2Int numCellsPerAxis, float cellSize) {
            return new Vector2Int(
                Mathf.FloorToInt((pos.x - (gridCenter.x - (numCellsPerAxis.x * cellSize)/2f))/cellSize),
                Mathf.FloorToInt((pos.z - (gridCenter.z - (numCellsPerAxis.y * cellSize)/2f))/cellSize)
            );
        }

        public static int GetProjectedIndex(int x, int y, Vector2Int numCellsPerAxis) {
            return x*numCellsPerAxis.y + y;
        }
        public static int GetProjectedIndex(Vector2Int xy, Vector2Int numCellsPerAxis) {
            return xy.x*numCellsPerAxis.y + xy.y;
        }

        public static int GetProjectedIndex(Vector3 pos, Vector3 gridCenter, Vector2Int numCellsPerAxis, float cellSize) {
            Vector2Int xy = GetGridXY(pos, gridCenter, numCellsPerAxis, cellSize);
            return GetProjectedIndex(xy.x, xy.y, numCellsPerAxis);
        }

        [System.Serializable]
        public class Grid2DCell {
            public RVOGrid2D grid;
            public int2 index;
            public int projectedIndex;
            public List<int> neighborCellsIndices;
            public List<RVO_Robot> agents;
            public int numAgents => agents.Count;

            public Grid2DCell(RVOGrid2D grid, int x, int y, int projectedIndex) {
                this.grid = grid;
                this.index = new(x,y);
                this.projectedIndex = projectedIndex;
                this.agents = new List<RVO_Robot>();
                neighborCellsIndices = new List<int>();
                for(int xi = Mathf.Max(x-1,0); xi <= Mathf.Min(x+1,grid.numCellsPerAxis.x-1); xi++) {
                    for(int yi = Mathf.Max(y-1,0); yi <= Mathf.Min(y+1,grid.numCellsPerAxis.y-1); yi++) {
                        if (xi == x && yi == y) continue;
                        neighborCellsIndices.Add(Grid.GetProjectedIndex(xi,yi,grid.numCellsPerAxis));
                    }
                }
            }

            public void ClearCell() {
                this.agents = new List<RVO_Robot>();
            }

            public void AddAgent(RVO_Robot agent) {
                this.agents.Add(agent);
            }

            public List<RVO_Robot> GetAgents() {
                return this.agents;
            }

            public List<RVO_Robot> GetNeighborAgents() {
                List<RVO_Robot> all_agents = new List<RVO_Robot>(this.agents);
                if (this.grid == null) return all_agents;

                foreach(int pi in this.neighborCellsIndices) {
                    all_agents.AddRange(this.grid.cells[pi].GetAgents());
                }
                return all_agents;
            }
        }
    
        [System.Serializable]
        public class Grid2D<T> {

            [System.Serializable]
            public class Cell {
                public Grid2D<T> parentGrid;
                public Vector2Int index;
                public int projectedIndex;
                public List<int> neighborCellsIndices;
                public List<T> agents;
                public int numAgents => agents.Count;
                
                public Cell(Grid2D<T> parentGrid, int x, int y, int projectedIndex) {
                    this.parentGrid = parentGrid;
                    this.index = new Vector2Int(x,y);
                    this.projectedIndex = projectedIndex;
                    this.agents = new List<T>();
                    neighborCellsIndices = new List<int>();
                    
                    for(int xi = Mathf.Max(x-1,0); xi <= Mathf.Min(x+1,parentGrid.numCellsPerAxis.x-1); xi++) {
                        for(int yi = Mathf.Max(y-1,0); yi <= Mathf.Min(y+1,parentGrid.numCellsPerAxis.y-1); yi++) {
                            if (xi == x && yi == y) continue;
                            neighborCellsIndices.Add(RVO.Grid.GetProjectedIndex(xi,yi,parentGrid.numCellsPerAxis));
                        }
                    }
                }

                public void ClearCell() {       this.agents = new List<T>();    }
                public void AddAgent(T agent) { this.agents.Add(agent);         }
                public List<T> GetAgents() {    return this.agents;             }

                public List<T> GetNeighborAgents() {
                    List<T> all_agents = new List<T>(this.agents);
                    if (this.parentGrid == null) return all_agents;
                    foreach(int pi in this.neighborCellsIndices) {
                        all_agents.AddRange(this.parentGrid.cells[pi].GetAgents());
                    }
                    return all_agents;
                }
            }

            [Header("=== STATIC VARIABLES ===")]
            [SerializeField, ReadOnlyInsp] private Vector3 lowerBound3D;
            [SerializeField, ReadOnlyInsp] private Vector3 upperBound3D;
            public Vector2 lowerBound => lowerBound3D.ToVector2();
            public Vector2 upperBound => upperBound3D.ToVector2();
            public float gridCellSize = 1f;
    
            [Header("=== GRID DATA ===")]
            [SerializeField, ReadOnlyInsp] private Vector3 _center = Vector3.zero;
            public Vector3 center => _center;
            [SerializeField, ReadOnlyInsp] private Vector2 _dimensions = Vector2.zero;
            public Vector2 dimensions => _dimensions;
            [SerializeField, ReadOnlyInsp] private int _numCells = 0;
            public int numCells => _numCells;
            [SerializeField, ReadOnlyInsp] private Vector2Int _numCellsPerAxis = Vector2Int.zero;
            public Vector2Int numCellsPerAxis => _numCellsPerAxis;


            private Vector3 upwardDir = Vector3.up;
            [SerializeField] private Cell[] _cells;
            public Cell[] cells => _cells;

            /*
            [Header("=== DEBUG ===")]
            public bool gizmos_center = true;
            public bool gizmos_orthogonal = true;
            public bool gizmos_cells = true;
            private bool gizmos => gizmos_center || gizmos_orthogonal || gizmos_cells;

            void OnDrawGizmos() {
                if (!gizmos) return;
                if (gizmos_center) {
                    // Draw the center of the grid
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(_center, 0.25f);
                }
                if (gizmos_orthogonal) {
                    // Draw orthogonal norms of 2D flat grid
                    Handles.DrawBezier(center-upwardDir*5f, center+upwardDir*5f, center-upwardDir*5f, center+upwardDir*5f, Color.yellow, null, 3);
                }
                if (gizmos_cells) {
                    // Draw inidividual cells
                    for(int x = 0; x < _numCellsPerAxis.x; x++) {
                        for(int y = 0; y < _numCellsPerAxis.y; y++) {
                            Vector3 v1 = lowerRef.position + new Vector3(gridCellSize*x, 0.25f, gridCellSize*y);
                            Vector3 v2 = v1 + new Vector3(gridCellSize, 0f, 0f);
                            Vector3 v3 = v2 + new Vector3(0f, 0f, gridCellSize);
                            Vector3 v4 = v3 + new Vector3(-gridCellSize, 0f, 0f);
                            Vector3[] verts = new Vector3[]{v1,v2,v3,v4};
                            int projectedIndex = x*_numCellsPerAxis.y + y;
                            Handles.DrawSolidRectangleWithOutline(
                                verts, 
                                //new Color(1f, 0f, 0f, Mathf.Clamp((float)cells[projectedIndex].numAgents/3f,0f,1f)), 
                                new Color(1f, 0f, 0f, 0.1f), 
                                new Color(0, 0, 0, 1)
                            );
                        }
                    }
                }
            }
            */

            public void SetLowerBound(Vector3 p) {      lowerBound3D = p;   }
            public void SetUpperBound(Vector3 p) {      upperBound3D = p;   }
            public void SetBounds(Vector3 lower, Vector3 upper) {
                Debug.Log("SETTING STUFF");
                SetLowerBound(lower);
                SetUpperBound(upper);
            }

            public bool Initialize() {
                Vector2 diff = upperBound - lowerBound;
                if (diff.magnitude == 0) return false;
                _dimensions = new Vector2(Mathf.Abs(diff.x), Mathf.Abs(diff.y));
                _center = (lowerBound3D + upperBound3D)/2f;
                _numCellsPerAxis = new Vector2Int(
                    Mathf.FloorToInt(_dimensions.x / gridCellSize),
                    Mathf.FloorToInt(_dimensions.y / gridCellSize)
                );
                _numCells = _numCellsPerAxis.x * _numCellsPerAxis.y;
                _cells = new Cell[_numCells];
                for(int x = 0; x < _numCellsPerAxis.x; x++) {
                    for(int y = 0; y < _numCellsPerAxis.y; y++) {
                        int projectedIndex = RVO.Grid.GetProjectedIndex(x,y,_numCellsPerAxis);
                        _cells[projectedIndex] = new Cell(this, x, y, projectedIndex);
                    }
                }
                return true;
            }

            public void ClearGrid() {
                foreach(Cell cell in _cells) cell.ClearCell();
            }
            public Vector2Int GetGridXY(Vector3 position) {
                return RVO.Grid.GetGridXY(position, _center, _numCellsPerAxis, gridCellSize);
            }
            public int GetProjectedIndex(Vector2Int xy) {
                return RVO.Grid.GetProjectedIndex(xy, _numCellsPerAxis);
            }
            public int GetProjectedIndex(int x, int y) {
                return RVO.Grid.GetProjectedIndex(new Vector2Int(x,y), _numCellsPerAxis);
            }
            public int GetProjectedIndex(Vector3 position) {
                return RVO.Grid.GetProjectedIndex(position, _center, _numCellsPerAxis, gridCellSize);
            }
            public void UpdateCell(int projectedIndex, T robot) {
                _cells[projectedIndex].AddAgent(robot);
            }
            public int UpdateCell(T robot, Vector3 pos) {
                int projectedIndex = GetProjectedIndex(pos);
                UpdateCell(projectedIndex, robot);
                return projectedIndex;
            }
            public void ResetGrid() {
                _center = Vector3.zero;
                _dimensions = Vector2Int.zero;
                _numCells = 0;
                _numCellsPerAxis = Vector2Int.zero;
                _cells = new Cell[0];
            }
        }
    }
    
    public static class Utils {
        public static Vector2[] CreateDirections2D(int n) {
            return CreateDirections2D(n, Mathf.Sqrt(Random.value));
        }
        public static Vector2[] CreateDirections2D(int n, float r) {
            Vector2[] directions = new Vector2[n];
            float angleStep = 2f*Mathf.PI / n;
            for(int i = 0; i < n; i++) {
                //float theta = (i+0.5f) * Mathf.PI * (1f + Mathf.Sqrt(5f));
                float theta = i * angleStep;
                directions[i] = new Vector2(r * Mathf.Sin(theta), r * Mathf.Cos(theta));
            }
            return directions;
        }
        public static float2[] CreateDirectionsFloat2(int n, float r) {
            float2[] directions = new float2[n];
            float angleStep = 2f*Mathf.PI / n;
            for(int i = 0; i < n; i++) {
                float theta = i * angleStep;
                directions[i] = (float2)new(r * Mathf.Sin(theta), r * Mathf.Cos(theta));
            }
            return directions;
        }

        public static bool InBetween(float theta_dif, float theta_left, float theta_right) {
            // There is a profound issue: we can have negative thetas.
            // Firstly, add all vectors by 360
            float dif = theta_dif + 360f;
            float l = theta_left + 360f;
            float r = theta_right + 360f;
            // Second, check if R > L or L < R
            if (r > l) {
                // This produces an OR condition: either dif >= r or dif <= l
                return (dif >= r || dif <= l);
            } 
            // This produces an AND condition: r <= dif <= l
            return (r <= dif && dif <= l );
        }

        public static bool VelInVO(Vector2 p0, Vector2 pA, float theta_left, float theta_right, Vector2 vQuery) {
            // pA = the original position of the current agent
            // p0 = the translated pA
            // theta_left = the angle of the left bound
            // theta_right = the angle of the right bound
            // vQuery = our query velocity.
            // Idea: can we check if our query velocity is contained within our left and right bounds?

            Vector2 dif = vQuery + pA - p0;                                 // Get a new velocity based on the translation
            float theta_dif = Mathf.Atan2(dif.y, dif.x);                    // Calculate the angles involved, all relative to the + x-axis
            return InBetween(theta_dif, theta_left, theta_right); // Check if the angle of dif is between the angle of theta_right and theta_left
        }
        public static bool VelInVO(float2 p0, float2 pA, float theta_left, float theta_right, float2 vQuery) {
            // pA = the original position of the current agent
            // p0 = the translated pA
            // theta_left = the angle of the left bound
            // theta_right = the angle of the right bound
            // vQuery = our query velocity.
            // Idea: can we check if our query velocity is contained within our left and right bounds?

            float2 dif = vQuery + pA - p0;                                 // Get a new velocity based on the translation
            float theta_dif = math.atan2(dif[1], dif[0]);                    // Calculate the angles involved, all relative to the + x-axis
            return InBetween(theta_dif, theta_left, theta_right); // Check if the angle of dif is between the angle of theta_right and theta_left
        }

        public static float GetPenalty(Vector2 vDesired, Vector2 vQuery, float timeCost, float aggressiveness = 1f) {
            // vDesired = The desired velocity of the current agent
            // vQuery = our query velocity
            // timeCost = the cost relative to time if we were to travel towards vQuery
            // aggressiveness = the current agent's tendency to aggresively favor their desired velocity
            return aggressiveness/timeCost + (vDesired - vQuery).magnitude;
        }
    }

    /*
    public class RVO {
        //from math import ceil, floor, sqrt
        // import copy
        //import numpy
        // from math import cos, sin, tan, atan2, asin
        // from math import pi as PI

        public static float Distance(Vector2 pos1, Vector2 pos2, float epsilon=0.001f) {
            // compute Euclidean distance for 2D
            return Vector2.Distance(pos1,pos2) + epsilon;
            // return sqrt((pose1[0]-pose2[0])**2+(pose1[1]-pose2[1])**2)+epsilon
        }
        
        public static float Distance(Vector3 pos1, Vector3 pos2, float epsilon=0.001f) {
            return Vector3.Distance(pos1,pos2) + epsilon;
        }

        public static void RVO_Optimal(
            int index,
            Vector2 pA,
            Vector2 vA,
            Vector2 desiredVelocity,
            float radius,
            List<Vector2> otherPositions,
            List<Vector2> otherVelocities
        ) {
            float ROB_RAD = radius+0.1f;
            List<RVO_BA> RVO_BA_all = new List<RVO_BA>();
            Vector2 optimalVelocity = vA;
            for(int i = 0; i < otherPositions.Count; i++) {
                if (index == i) continue;
                Vector2 pB = otherPositions[i];
                Vector2 vB = otherVelocities[i];
                // RVO
                Vector2 transl_vB_vA = pA + 0.5f * (vB + vA);
                // VO
                // Vector2 transl_vB_vA = new Vector2(pA.x + vB.x, pA.y + vB.y);
                float dist_BA = Distance(pA, pB);
                float theta_BA = Mathf.Atan2(pB.y-pA.y, pB.x-pA.x);
                if (2f * ROB_RAD > dist_BA) dist_BA = 2f * ROB_RAD;
                float theta_BAort = Mathf.Asin(2f * ROB_RAD / dist_BA);
                float theta_ort_left = theta_BA + theta_BAort;
                Vector2 bound_left = new Vector2(
                    Mathf.Cos(theta_ort_left), 
                    Mathf.Sin(theta_ort_left)
                );
                float theta_ort_right = theta_BA - theta_BAort;
                Vector2 bound_right = new Vector2(
                    Mathf.Cos(theta_ort_right), 
                    Mathf.Sin(theta_ort_right)
                );
                // HRVO
                // dist_dif = distance([0.5*(vB[0]-vA[0]),0.5*(vB[1]-vA[1])],[0,0])
                // transl_vB_vA = [pA[0]+vB[0]+cos(theta_ort_left)*dist_dif, pA[1]+vB[1]+sin(theta_ort_left)*dist_dif]
                RVO_BA_all.Add(new RVO_BA(transl_vB_vA, bound_left, bound_right, vB, dist_BA, 2*ROB_RAD));
            }

        }

        /*
        public static List<Vector2> RVO_update(
            List<Vector2> positions, 
            List<Vector2> desiredVelocities, 
            List<Vector2> currentVelocities, 
            float radius
        ) {
            // compute best velocity given the desired velocity, current velocity and workspace model
            float ROB_RAD = radius+0.1f;
            List<Vector2> optimalVelocities = new List<Vector2>(currentVelocities);
            for(int i = 0; i < positions.Count; i++) {
                Vector2 vA = currentVelocities[i];
                Vector2 pA = positions[i];
                List<RVO_BA> RVO_BA_all = new List<RVO_BA>();
                for(int j = 0; j < positions.Count; j++) {
                    if (i == j) continue;
                    Vector2 vB = currentVelocities[j];
                    Vector2 pB = positions[j];
                    // RVO
                    Vector2 transl_vB_vA = new Vector2(
                        pA.x + 0.5f * (vB.x+vA.x), 
                        pA.y + 0.5f * (vB.y+vA.y)
                    );
                    // VO
                    // Vector2 transl_vB_vA = new Vector2(pA.x + vB.x, pA.y + vB.y);
                    float dist_BA = Distance(pA, pB);
                    float theta_BA = Mathf.Atan2(pB.y-pA.y, pB.x-pA.x);
                    if (2f * ROB_RAD > dist_BA) dist_BA = 2f * ROB_RAD;
                    float theta_BAort = Mathf.Asin(2f * ROB_RAD / dist_BA);
                    float theta_ort_left = theta_BA + theta_BAort;
                    Vector2 bound_left = new Vector2(
                        Mathf.Cos(theta_ort_left), 
                        Mathf.Sin(theta_ort_left)
                    );
                    float theta_ort_right = theta_BA - theta_BAort;
                    Vector2 bound_right = new Vector2(
                        Mathf.Cos(theta_ort_right), 
                        Mathf.Sin(theta_ort_right)
                    );
                    // HRVO
                    // dist_dif = distance([0.5*(vB[0]-vA[0]),0.5*(vB[1]-vA[1])],[0,0])
                    // transl_vB_vA = [pA[0]+vB[0]+cos(theta_ort_left)*dist_dif, pA[1]+vB[1]+sin(theta_ort_left)*dist_dif]
                    RVO_BA_all.Add(new RVO_BA(transl_vB_vA, bound_left, bound_right, dist_BA, 2*ROB_RAD));                
                }
                
                //for hole in ws_model['circular_obstacles']:
                //    # hole = [x, y, rad]
                //    vB = [0, 0]
                //    pB = hole[0:2]
                //    transl_vB_vA = [pA[0]+vB[0], pA[1]+vB[1]]
                //    dist_BA = distance(pA, pB)
                //    theta_BA = atan2(pB[1]-pA[1], pB[0]-pA[0])
                //    # over-approximation of square to circular
                //    OVER_APPROX_C2S = 1.5
                //    rad = hole[2]*OVER_APPROX_C2S
                //    if (rad+ROB_RAD) > dist_BA:
                //        dist_BA = rad+ROB_RAD
                //    theta_BAort = asin((rad+ROB_RAD)/dist_BA)
                //    theta_ort_left = theta_BA+theta_BAort
                //    bound_left = [cos(theta_ort_left), sin(theta_ort_left)]
                //    theta_ort_right = theta_BA-theta_BAort
                //    bound_right = [cos(theta_ort_right), sin(theta_ort_right)]
                //    RVO_BA = [transl_vB_vA, bound_left, bound_right, dist_BA, rad+ROB_RAD]
                //    RVO_BA_all.append(RVO_BA)
                
                Vector2 vA_post = Intersect(pA, desiredVelocities[i], RVO_BA_all);
                optimalVelocities[i] = vA_post;
            }
            return optimalVelocities;
        }

        public static Vector2 Intersect(Vector2 pA, Vector2 vA, List<RVO_BA> RVO_BA_all) {
            float norm_v = Distance(vA, Vector2.zero);
            List<Vector2> suitableVelocities = new List<Vector2>();
            List<Vector2> unsuitableVelocities = new List<Vector2>();
            for (float theta = 0f; theta < 2f*Mathf.PI; theta += 0.1f) {
                for(float rad = 0.02f; rad < norm_v+0.02f; rad += norm_v/5f) {
                    Vector2 new_v = new Vector2(
                        rad*Mathf.Cos(theta), 
                        rad*Mathf.Sin(theta)
                    );
                    bool suit = true;
                    foreach(RVO_BA rvo_ba in RVO_BA_all) {
                        Vector2 p_0 = rvo_ba.transl_vB_vA;
                        Vector2 left = rvo_ba.bound_left;
                        Vector2 right = rvo_ba.bound_right;
                        Vector2 dif = new Vector2(
                            new_v.x + pA.x - p_0.x, 
                            new_v.y + pA.y - p_0.y
                        );
                        float theta_dif = Mathf.Atan2(dif.y, dif.x);
                        float theta_right = Mathf.Atan2(right.y, right.x);
                        float theta_left = Mathf.Atan2(left.y, left.x);
                        if (in_between(theta_right, theta_dif, theta_left)) {
                            suit = false;
                            break;
                        }
                    }
                    if (suit) suitableVelocities.Add(new_v);
                    else unsuitableVelocities.Add(new_v); 
                }
            }

            Vector2 new_v = vA;
            bool suit = true;
            foreach(RVO_BA rvo_ba in RVO_BA_all) {
                Vector2 p_0 = rvo_ba.transl_vB_vA;
                float left = rvo_ba.bound_left;
                float right = rvo_ba.bound_right;
                Vector2 dif = new Vector2(
                    new_v.x + pA.x - p_0.x, 
                    new_v.y + pA.y - p_0.y
                );
                float theta_dif = Mathf.Atan2(dif.y, dif.x);
                float theta_right = Mathf.Atan2(right.y, right.x);
                float theta_left = Mathf.Atan2(left.y, left.x);
                if (in_between(theta_right, theta_dif, theta_left)) {
                    suit = false;
                    break;
                }
            }                
            if (suit) suitableVelocities.Add(new_v);
            else unsuitableVelocities.Add(new_v);

            if (suitable_V.Count > 0) {
                // Suitable found
                float min_angle = Vector2.Dot(suitableVelocities[0], vA);
                float best_v = suitableVelocities[0];
                foreach(Vector2 pot_v in suitableVelocities) {
                    float cur_angle = Vector2.Dot(pot_v, vA);
                    if (cur_angle < min_angle) {
                        best_v = pot_v;
                        min_angle = cur_angle;
                    }
                }
                new_v = best_v;
                
                //for RVO_BA in RVO_BA_all:
                //    p_0 = RVO_BA[0]
                //    left = RVO_BA[1]
                //    right = RVO_BA[2]
                //    dif = [new_v[0]+pA[0]-p_0[0], new_v[1]+pA[1]-p_0[1]]
                //    theta_dif = atan2(dif[1], dif[0])
                //    theta_right = atan2(right[1], right[0])
                //    theta_left = atan2(left[1], left[0])
                
            } else {
                // Suitable not found
                Dictionary<Vector2, int> tc_V = new Dictionary<Vector2, int>();
                foreach(Vector2 unsuitV in unsuitableVelocities) {
                    tc_V.Add(unsuitV, 0);
                    List<float> tc = new List<float>();
                    foreach(RVO_BA rvo_ba in RVO_BA_all) {
                        Vector2 p_0 = rvo_ba.transl_vB_vA;
                        float left = rvo_ba.bound_left;
                        float right = rvo_ba.bound_right;
                        float dist = rvo_ba.dist_BA;
                        float rad = rvo_ba.rad;
                        Vector2 dif = new Vector2(
                            unsuitV.x + pA.x-p_0.x, 
                            unsuitV.y + pA.y-p_0.y
                        );
                        float theta_dif = Mathf.Atan2(dif.y, dif.x);
                        float theta_right = Mathf.Atan2(right.y, right.x);
                        float theta_left = Mathf.Atan2(left.y, left.x);
                        if (in_between(theta_right, theta_dif, theta_left)) {
                            float small_theta = Mathf.Abs(theta_dif - 0.5f*(theta_left+theta_right));
                            if (Mathf.Abs(dist*Mathf.Sin(small_theta)) >= rad) rad = Mathf.Abs(dist*Mathf.Sin(small_theta));
                            float big_theta = Mathf.Asin(Mathf.Abs(dist*Mathf.Sin(small_theta))/rad);
                            float dist_tg = Mathf.Abs(dist*Mathf.Cos(small_theta))-Mathf.Abs(rad*Mathf.Cos(big_theta));
                            if (dist_tg < 0f) dist_tg = 0f;                   
                            float tc_v = dist_tg/Vector2.Distance(dif, Vector2.zero);
                            tc.Add(tc_v);
                        }
                    }
                    tc_V[unsuitV] = tc.Min()+0.001f;
                }
                float WT = 0.2f;
                float min_angle = (WT/tc_v[unsuitableVelocities[0]]) + Vector2.Distance(unsuitableVelocities[0],vA);
                float best_v = unsuitableVelocities[0];
                foreach(Vector2 pot_v in unsuitableVelocities) {
                    float cur_angle = ((WT/tc_V[pot_v])+distance(pot_v, vA));
                    if (cur_angle < min_angle) {
                        best_v = pot_v;
                        min_angle = cur_angle;
                    }
                }
                vA_post = best_v;
            }
        
            return vA_post; 
        }

        public static bool in_between(float theta_right, float theta_dif, float theta_left) {
            if (Mathf.Abs(theta_right - theta_left) <= Mathf.PI) {
                return (theta_right <= theta_dif && theta_dif <= theta_left);
            }
            if (theta_left < 0f && theta_right > 0f) {
                theta_left += 2f*Mathf.PI;
                if (theta_dif < 0f) theta_dif += 2f*Mathf.PI;
                return (theta_right <= theta_dif && theta_dif <= theta_left);
            }
            if (theta_left > 0f && theta_right < 0f) {
                theta_right += 2f*Mathf.PI;
                if (theta_dif < 0f) theta_dif += 2f*Mathf.PI;
                return (theta_left <= theta_dif && theta_dif <= theta_right);
            }
        }

        public static List<Vector2> compute_V_des(List<Vector2> positions, List<Vector2> goal, Vector2 V_max) {
            List<Vector2> V_des = new List<Vector2>();
            for(int i = 0; i < X.Count; i++) {
                Vector2 dif_x = new Vector2(
                    goal[i].x - X[i].x,
                    goal[i].y - X[i].y
                );
                float norm = Vector2.Distance(dif_x, Vector2.zero);
                Vector2 norm_dif_x = new Vector2(
                    dif_x.x * V_max.x/norm,
                    dif_x.y * V_max.y/norm
                );
                if (Vector2.Distance(positions[i],goal[i]) <= 0.1f) norm_dif_x = Vector2.zero;
                V_des.Add(norm_dif_x);
            }
            return V_des;
        }

    }
    */  
}
