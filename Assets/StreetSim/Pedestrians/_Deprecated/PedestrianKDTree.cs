using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DataStructures.ViliWonka.KDTree;

namespace StreetSim {

    public class PedestrianKDTree : MonoBehaviour
    {
        // ==========================================
        // === STATIC INSTANCE ===
        // ==========================================
        public static PedestrianKDTree Instance;

        public Vector3 point_size = new Vector3(1f,1f,1f);
        public List<RVOEntity> obstacles;
        private Vector3[] pointCloud;
        private Transform[] pointTransforms;
        private List<int> result_indices = new List<int>();

        public Transform queryPedestrian;
        [Range(0f,50f)] public float query_radius = 5f;
        
        private KDTree tree;
        private KDQuery query;

        bool builtThisFrame;

        private void OnDrawGizmos() {
            if (queryPedestrian != null) {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(queryPedestrian.position, query_radius);

                if (result_indices.Count > 0) {
                    Gizmos.color = Color.blue;
                    for(int i = 0; i < result_indices.Count; i++) {
                        Gizmos.DrawLine(queryPedestrian.position, pointTransforms[result_indices[i]].position);
                    }
                }
            }
        }

        private void Awake() {
            Instance = this;
            getPoints();
            query = new KDQuery();
            tree = new KDTree(pointCloud, 32);
        }

        public void AddObstacle(RVOEntity obstacle) {
            obstacles.Add(obstacle);
            getPoints();
            tree = new KDTree(pointCloud, 32);
        }

        public void RemoveObstacle(RVOEntity obstacle) {
            obstacles.Remove(obstacle);
            getPoints();
            tree = new KDTree(pointCloud, 32);
        }

        public Vector3[] getPoints() {
            pointCloud = new Vector3[obstacles.Count];
            pointTransforms = new Transform[obstacles.Count];
            for(int i = 0; i < obstacles.Count; i++)
            {
                if(obstacles[i] == null) {
                    RemoveObstacle(obstacles[i]);
                    i -= 1;
                    continue;
                }
                pointCloud[i] = obstacles[i].transform.position;
                pointTransforms[i] = obstacles[i].transform;
                pointTransforms[i].localScale = point_size;
            }
            return pointCloud;
        }

        public void FillAndBuild() {
            for (int i = 0; i < obstacles.Count; i++) {
                tree.Points[i] = obstacles[i].transform.position;
                pointTransforms[i].localScale = point_size;
            }
            tree.Rebuild();
            builtThisFrame = true;
        }

        public void DoRadiusQuery(Vector3 queryPosition, float queryRadius, List<int> resultIndices) {
            if(!builtThisFrame) {
                FillAndBuild();
            }
            query.Radius(tree, queryPosition, queryRadius, resultIndices);
        }

        void Update() {
            FillAndBuild();
            builtThisFrame = false;

            // Let's do a test of our own
            if (queryPedestrian == null) return;
            result_indices = new List<int>();
            DoRadiusQuery(queryPedestrian.position, query_radius, result_indices);
        }
    }
}
