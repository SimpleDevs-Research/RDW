using System.Collections.Generic;
using UnityEngine;
using DataStructures.ViliWonka.KDTree;

namespace StreetSim {
    public class DynamicKDTree : MonoBehaviour
    {   
        [Header("=== Tree Preparation ===")]
        [SerializeField, Tooltip("List of active entities. Whenever this is changed (which is only possible via the inspector, `AddEntity()` or `RemoveEntity()`, we also update our tree.")]
        private List<Entity> _entities = new();
        private HashSet<Entity> _entitiesHash = new();
        private Vector3[] _pointCloud;

        public List<Entity> entities => _entities;

        [Header("=== DEBUGGING ===")]
        [SerializeField, Tooltip("Just slap on a Transform to start querying closest within a radius.")]
        private Transform _debugTransform;
        [SerializeField, Tooltip("Radius for the debug querying.")]
        private float _debugRadius = 5f;

        protected KDTree _tree = null;
        protected KDQuery _query = null;

        protected virtual void Awake() {
            // Pre-emptively initialize our query
            _query = new KDQuery();
            
            // As a first step, we update our point cloud.
            // If this is successful, we will automatically update our tree.
            TryUpdatePointCloud();
        }

        protected virtual void Update() {
            FillAndRebuild();
        }

        public virtual bool TryUpdatePointCloud() {
            // can't build a point cloud if there's no entities!
            if (_entities.Count == 0) {
                Debug.LogWarning("Cannot build a KDTree with no entities!");
                _tree = null;
                return false;
            }

            // Initialize our point cloud as a list first. And our hashset. This'll become obvious later
            List<Entity> newEntities = new();
            List<Vector3> newPoints = new();
            HashSet<Entity> newEntitiesHash = new();

            // Loop through our list of entities.
            for(int i = 0; i < _entities.Count; i++) {
                Entity e = _entities[i];
                if (e != null) {
                    e.onEnabled.RemoveListener(TryAddEntity);
                    e.onDisabled.RemoveListener(TryRemoveEntity);
                    if(newEntitiesHash.Add(e)) {
                        // Basically ensure that we aren't looking at duplicates
                        newEntities.Add(e);
                        newPoints.Add(e.transform.position);
                        e.onEnabled.AddListener(TryAddEntity);
                        e.onDisabled.AddListener(TryRemoveEntity);
                    }
                }
            }

            // Update our entities list and poiont cloud array
            _entities = newEntities;
            _entitiesHash = newEntitiesHash;
            _pointCloud = newPoints.ToArray();
            
            // If pointCloud is empty, then there's nothing we can do.
            if (newPoints.Count == 0) {
                Debug.LogError("Cannot build a KDTree with no entities... after filtering.");
                _tree = null;
                return false;
            }

            // Only at this point do we generate the tree
            _tree = new KDTree(_pointCloud, 32);
            return true;
        }

        public virtual void FillAndRebuild() {
            if (_tree == null) {
                Debug.LogWarning("Cannot fill and rebuild tree if null");
                return;
            }
            for (int i = 0; i < _entities.Count; i++) {
                _tree.Points[i] = _entities[i].transform.position;
            }
            _tree.Rebuild();
        }

        public virtual void TryAddEntity(Entity e) {
            if (_entitiesHash.Add(e)) {
                _entities.Add(e);
                TryUpdatePointCloud();
            }
        }
        public virtual void TryRemoveEntity(Entity e) {
            if (_entitiesHash.Remove(e)) {
                _entities.Remove(e);
                TryUpdatePointCloud();
            }
        }

        public virtual bool QueryRadius(Vector3 queryPosition, float queryRadius, List<int> resultIndices) {
            if (_tree == null) {
                Debug.LogError("Cannot query radius: tree is not built");
                return false;
            }
            _query.Radius(_tree, queryPosition, queryRadius, resultIndices);
            return true;
        }

        private void OnDestroy() {
            foreach(Entity e in _entitiesHash) {
                e.onEnabled.RemoveListener(TryAddEntity);
                e.onDisabled.RemoveListener(TryRemoveEntity);
            }
        }

        private void OnDrawGizmos() {
            if (!Application.isPlaying || _debugTransform == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_debugTransform.position, _debugRadius);

            List<int> resultIndices = new();
            if (QueryRadius(_debugTransform.position, _debugRadius, resultIndices)) {
                if (resultIndices.Count > 0) {
                    Gizmos.color = Color.blue;
                    foreach(int i in resultIndices) {
                        Gizmos.DrawLine(_debugTransform.position, _pointCloud[i]);
                    }
                }
            }
        }
    }
}
