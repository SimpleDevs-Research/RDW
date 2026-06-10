using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSWriter : MonoBehaviour
{

    public CSVWriter writer;
    public float fps_smoothing_factor = 0.25f;

    private float current_fps;
    private float smoothed_fps;

    private void Start() {
        writer.Initialize();
    }

    private void Update() {
        int frame = Time.frameCount;
        float dt = Time.unscaledDeltaTime;
        current_fps = 1f / dt;
        smoothed_fps = (fps_smoothing_factor * current_fps) + (1f - fps_smoothing_factor) * smoothed_fps;
        writer.AddPayload(frame);
        writer.AddPayload((int)current_fps);
        writer.AddPayload((int)smoothed_fps);
        writer.AddPayload(dt);
        writer.WriteLine(true);
    }

    private void OnDestroy() {
        writer.Disable();
    }
}
