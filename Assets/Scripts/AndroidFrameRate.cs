using UnityEngine;
using System.Collections;

public class AndroidFrameRate : MonoBehaviour
{
    [SerializeField] private int targetFPS = 120;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
    }

    private IEnumerator Start()
    {
        while (true)
        {
            int frames = 0;
            float elapsed = 0f;

            while (elapsed < 2f)
            {
                yield return null;

                frames++;
                elapsed += Time.unscaledDeltaTime;
            }

            float fps = frames / elapsed;

            Debug.Log(
                $"[FPS_DIAGNOSTIC] " +
                $"Requested={Application.targetFrameRate}, " +
                $"DisplayHz={Screen.currentResolution.refreshRateRatio.value:F2}, " +
                $"MeasuredFPS={fps:F2}"
            );
        }
    }
}