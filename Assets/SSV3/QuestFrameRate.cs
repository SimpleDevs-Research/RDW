using UnityEngine;
using System.Collections;

public class QuestFrameRate : MonoBehaviour
{
    public float targetHz = 120f;

    private IEnumerator Start()
    {
        // Wait for Meta XR initialization.
        while (OVRManager.display == null)
            yield return null;

        // Request 120 Hz.
        OVRManager.display.displayFrequency = targetHz;

        yield return new WaitForSecondsRealtime(2f);

        Debug.Log(
            $"[QUEST_FPS] Requested={targetHz} Hz, " +
            $"Actual={OVRManager.display.displayFrequency} Hz"
        );
    }
}