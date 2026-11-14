using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StreetSim {
    // Runs the full trial sequence automatically:
    //   20 -> 40 -> 60 -> 100 -> 200 -> 500, repeated `cycles` times (18 trials by default).
    // Each trial: wait until >= `activeThreshold` of num_agents are active, then run for `trialDurationSeconds`.
    // Between trials the trial scene is fully unloaded and reloaded with the next agent count.
    // After the last trial the application quits.
    //
    // No scene setup required: the first PedestrianGenerator (with `useTrialSequence` on) creates the
    // runner in its Awake. The runner persists across loads and reloads whichever scene created it.
    public class TrialRunner : MonoBehaviour
    {
        public static TrialRunner Instance { get; private set; }

        [Header("=== Sequence ===")]
        public int[] agentCounts = { 20, 40, 60, 100, 200, 500 };
        public int cycles = 3;

        [Header("=== Per-Trial Timing ===")]
        [Range(0f, 1f)] public float activeThreshold = 0.95f;
        public float trialDurationSeconds = 180f;

        [Header("=== Read-only ===")]
        [SerializeField] private int _trialIndex = 0;
        [SerializeField] private string _trialSceneName;
        [SerializeField] private bool _timerStarted = false;
        [SerializeField] private float _timeRemaining = 0f;
        [SerializeField] private bool _transitioning = false;

        private PedestrianGenerator _generator;

        [Header("=== HUD ===")]
        [Tooltip("Show a small head-locked readout of trial progress.")]
        public bool showHUD = true;

        public int TotalTrials => agentCounts.Length * cycles;
        public int CurrentAgentCount => agentCounts[_trialIndex % agentCounts.Length];

        // Read-only state for TrialHUD
        public int TrialNumber => _trialIndex + 1;
        public bool TimerStarted => _timerStarted;
        public float TimeRemaining => _timeRemaining;
        public int ActiveCount => _generator != null ? _generator.TotalActive : 0;
        public int TargetActiveCount => _generator != null
            ? Mathf.Max(1, Mathf.CeilToInt(_generator.num_agents * activeThreshold))
            : 0;

        // Called from PedestrianGenerator.Awake(). The first call creates the persistent runner.
        public static TrialRunner GetOrCreate() {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("TrialRunner");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TrialRunner>();
            Instance._trialSceneName = SceneManager.GetActiveScene().name;
            Instance.LogTrialStart();
            return Instance;
        }

        private void OnEnable()  { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void Start() {
            // The scene that created us is already loaded, so sceneLoaded won't fire for it.
            ResetTrialState();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (scene.name == _trialSceneName) ResetTrialState();
        }

        private void ResetTrialState() {
            _generator = FindAnyObjectByType<PedestrianGenerator>();
            _timerStarted = false;
            _timeRemaining = 0f;
            _transitioning = false;
            if (_generator == null) Debug.LogWarning("[TrialRunner] No PedestrianGenerator in trial scene.");
            // The HUD is parented to the scene's camera, so it's destroyed with the scene and rebuilt here.
            if (showHUD) TrialHUD.Create(this);
        }

        private void Update() {
            if (_transitioning || _generator == null) return;

            if (!_timerStarted) {
                if (_generator.TotalActive >= TargetActiveCount) {
                    _timerStarted = true;
                    _timeRemaining = trialDurationSeconds;
                    Debug.Log($"[TrialRunner] Trial {_trialIndex + 1}/{TotalTrials}: {_generator.TotalActive}/{_generator.num_agents} active. Running for {trialDurationSeconds}s.");
                }
                return;
            }

            _timeRemaining -= Time.unscaledDeltaTime;
            if (_timeRemaining <= 0f) {
                _timeRemaining = 0f;
                _transitioning = true;
                StartCoroutine(AdvanceTrial());
            }
        }

        private IEnumerator AdvanceTrial() {
            Debug.Log($"[TrialRunner] Trial {_trialIndex + 1}/{TotalTrials} ({CurrentAgentCount} agents) finished.");
            _trialIndex++;

            if (_trialIndex >= TotalTrials) {
                Debug.Log("[TrialRunner] All trials complete. Quitting.");
                Quit();
                yield break;
            }

            // Fully unload the current trial scene (Single mode unloads everything, which disables the
            // generator -> stops its recorder and disposes native arrays), then free leftover memory.
            _generator = null;
            LogTrialStart();
            AsyncOperation load = SceneManager.LoadSceneAsync(_trialSceneName, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        private void LogTrialStart() {
            Debug.Log($"[TrialRunner] Starting trial {_trialIndex + 1}/{TotalTrials} with {CurrentAgentCount} agents.");
        }

        private static void Quit() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
