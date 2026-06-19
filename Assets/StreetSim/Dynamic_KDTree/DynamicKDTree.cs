using System.Collections.Generic;
using UnityEngine;
using DataStructures.ViliWonka.KDTree;

namespace StreetSim {
    public class DynamicKDTree : MonoBehaviour
    {   
        [Header("=== Tree Preparation ===")]
        [SerializeField, Tooltip("List of active entities. Whenever this is changed (which is only possible via the inspector, `AddEntity()` or `RemoveEntity()`, we also update our tree.")]
        private List<GameObject> _trackables = new();
        [SerializeField, Tooltip("The Transform that acts as the parent. It's recommended to set this as the environment root transform (e.g. you want to translate the environment, and the tree must move along with it). If not set, it'll auto-set to this transform.")]
        private Transform _rootTransform;
        [SerializeField, Tooltip("When loaded during Awake, should we attempt to update the point cloud? Turn this OFF if you want to add elements to `entities` first")]
        private bool _updateOnAwake = true;

        [Header("=== DEBUGGING ===")]
        [SerializeField, Tooltip("Just slap on a Transform to start querying closest within a radius.")]
        private Transform _debugTransform;
        [SerializeField, Tooltip("Radius for the debug querying Radius search.")]
        private float _debugRadius = 5f;
        [SerializeField, Tooltip("K for debug querying KNearest search.")]
        private int _debugK = 3;
        [SerializeField, Tooltip("Should we yell out warnings and such? Mostly for debugging, toggle this on if you need to test thinsg out. Just don't forget to disable this in production.")]
        private bool _verboseMessages = false;

        protected KDTree _tree = null;
        protected KDQuery _query = null;
        private Vector3[] _pointCloud;
        private HashSet<GameObject> _trackablesHash = new();
        public List<GameObject> trackables => _trackables;

        protected virtual void Awake() {
            // Set root transform
            if (_rootTransform == null) _rootTransform = this.transform;

            // Pre-emptively initialize our query
            _query = new KDQuery();
            
            // As a first step, we update our point cloud.
            // If this is successful, we will automatically update our tree.
            if (_updateOnAwake) TryUpdatePointCloud();
        }

        // ==============================
        // This is a dynamic tree, so we gotta update our tree
        // ==============================
        protected virtual void Update() {
            FillAndRebuild();
        }

        // ==============================
        // We do this if we changed anything in our `_trackables` List.
        // This is different from rebuilding the tree, which has no change to the size
        // ==============================
        public virtual bool TryUpdatePointCloud() {
            // can't build a point cloud if there's no entities!
            if (_trackables.Count == 0) {
                if (_verboseMessages) Debug.LogWarning("Cannot build a KDTree with no trackables!");
                _tree = null;
                return false;
            }

            // Initialize our point cloud as a list first. And our hashset. This'll become obvious later
            List<GameObject> newTrackables = new();
            List<Vector3> newPoints = new();
            HashSet<GameObject> newTrackablesHash = new();

            // Loop through our list of entities.
            for(int i = 0; i < _trackables.Count; i++) {
                GameObject go = _trackables[i];
                if (go != null) {
                    EnableDisableNotifier edn = (go.GetComponent<EnableDisableNotifier>() == null) 
                        ? go.AddComponent<EnableDisableNotifier>()
                        : go.GetComponent<EnableDisableNotifier>();
                    edn.Enabled += TryAdd;
                    edn.Disabled += TryRemove;
                    if(newTrackablesHash.Add(go)) {
                        // Basically ensure that we aren't looking at duplicates
                        newTrackables.Add(go);
                        newPoints.Add(_rootTransform.InverseTransformPoint(go.transform.position));
                        edn.Enabled += TryAdd;
                        edn.Disabled += TryRemove;
                    }
                }
            }

            // Update our entities list and poiont cloud array
            _trackables = newTrackables;
            _trackablesHash = newTrackablesHash;
            _pointCloud = newPoints.ToArray();
            
            // If pointCloud is empty, then there's nothing we can do.
            if (newPoints.Count == 0) {
               if (_verboseMessages) Debug.LogError("Cannot build a KDTree with no trackables... after filtering.");
                _tree = null;
                return false;
            }

            // Only at this point do we generate the tree
            _tree = new KDTree(_pointCloud, 32);
            return true;
        }

        // ==============================
        // We do this if nothing has changed in the number of entities we want to track, 
        // but maybe some positions have changed.
        // ==============================
        public virtual void FillAndRebuild() {
            if (_tree == null) {
                if (_verboseMessages) Debug.LogWarning("Cannot fill and rebuild tree if null");
                return;
            }
            for (int i = 0; i < _trackables.Count; i++) {
                _tree.Points[i] = _rootTransform.InverseTransformPoint(_trackables[i].transform.position);
            }
            _tree.Rebuild();
        }

        // ==============================
        // Adders and Removers. Two variants exist: those that just pass GameObject, and those with GameObject + bool.
        // The 2nd bool ensures that you update the tree upon being added, and it's the default.
        // If you need more control and, for example, DON'T want to update the tree yet, then you must use the GameObject + bool variant.
        // ==============================

        public virtual void TryAdd(GameObject go, bool updateTree = true) {
            if (_trackablesHash.Add(go)) {
                _trackables.Add(go);
                if (updateTree) TryUpdatePointCloud();
            }
        }
        public virtual void TryAdd(GameObject go) { 
            TryAdd(go, true); 
        }
        public virtual void TryRemove(GameObject go, bool updateTree = true) {
            if (_trackablesHash.Remove(go)) {
                _trackables.Remove(go);
                if (updateTree) TryUpdatePointCloud();
            }
        }
        public virtual void TryRemove(GameObject go) { 
            TryRemove(go, true); 
        }

        // ==============================
        // Ways to query the tree. This expects a world position query. 
        // These will return the indices in `_entities` that are within 
        // the radius of the provided position. You'll have to decipher 
        // and process that on your own though.
        // ==============================
        public virtual bool QueryRadius(Vector3 worldPosition, float radius, List<int> resultIndices) {
            if (_tree == null) {
                if (_verboseMessages) Debug.LogError("Cannot query radius: tree is not built");
                return false;
            }
            _query.Radius(_tree, _rootTransform.InverseTransformPoint(worldPosition), radius, resultIndices);
            return true;
        }
        public bool QueryKNearest(Vector3 worldPosition, int k, List<int> resultIndices) {
            if (_tree == null) {
                if (_verboseMessages) Debug.LogError("Cannot query KNearest: tree is not built");
                return false;
            }
            _query.KNearest(_tree, _rootTransform.InverseTransformPoint(worldPosition), k, resultIndices);
            return true;
        }
        public bool QueryNearest(Vector3 worldPosition, out int closestIndex, out GameObject closestGameObject) {
            if (_tree == null) {
                closestIndex = -1;
                closestGameObject = null;
                if (_verboseMessages) Debug.LogError("Cannot query Nearest: tree is not built");
                return false;
            }
            List<int> resultIndices = new List<int>();
            _query.ClosestPoint(_tree, _rootTransform.InverseTransformPoint(worldPosition), resultIndices);
            closestIndex = resultIndices[0];
            closestGameObject = _trackables[closestIndex];
            return true;
        }

        private void OnDestroy() {
            foreach(GameObject go in _trackablesHash) {
                EnableDisableNotifier notifier = go.GetComponent<EnableDisableNotifier>();
                if (notifier != null) {
                    notifier.Enabled -= TryAdd;
                    notifier.Disabled -= TryRemove;
                }
            }
        }

        private void OnDrawGizmos() {
            if (!Application.isPlaying || _debugTransform == null) return;

            Vector3 debugLocalPos = _rootTransform.InverseTransformPoint(_debugTransform.position);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_rootTransform.TransformPoint(debugLocalPos), _debugRadius);
            
            List<int> radiusResults = new();
            if (QueryRadius(_debugTransform.position, _debugRadius, radiusResults)) {
                if (radiusResults.Count > 0) {
                    Gizmos.color = Color.blue;
                    foreach(int i in radiusResults) {
                        Gizmos.DrawLine(_rootTransform.TransformPoint(debugLocalPos), _rootTransform.TransformPoint(_pointCloud[i]));
                    }
                }
            }

            List<int> knearestResults = new();
            if (QueryKNearest(_debugTransform.position, _debugK, knearestResults)) {
                if (knearestResults.Count > 0) {
                    Gizmos.color = Color.green;
                    foreach(int k in knearestResults) {
                        Gizmos.DrawLine(_rootTransform.TransformPoint(debugLocalPos), _rootTransform.TransformPoint(_pointCloud[k]));
                    }
                }
            }

            if (QueryNearest(_debugTransform.position, out int j, out GameObject closestGameObject)) {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(_rootTransform.TransformPoint(debugLocalPos), closestGameObject.transform.position);
            }

        }
    }
}
