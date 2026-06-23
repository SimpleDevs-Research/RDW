using UnityEngine;

// ===========================
// This is a Singleton for managing seed randomization
// This ensure that even if things generate randomly, they'll 
// be CONSISTENTLY random and can be replicated if needed
// ===========================

namespace StreetSim {
    public class RandomSeedInitializer : MonoBehaviour
    {
        public static RandomSeedInitializer Instance { get; private set; }

        [Header("=== Seed Settings ===")]
        [Tooltip("Modify this integer in the inspector to change the randomization of the game.")]
        [SerializeField] private int customSeed = 42395;

        public int ActiveSeed => customSeed;

        private void Awake() {
            // Enforce singleton value
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            // Persistence
            DontDestroyOnLoad(gameObject);

            // Init global random state
            InitializeGlobalSeed();
        }

        private void InitializeGlobalSeed() {
            Random.InitState(customSeed);
            Debug.Log($"[SeedManager] Global Unity seed initialized to: {customSeed}");
        }
    }
}
