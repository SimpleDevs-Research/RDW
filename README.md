# Unity - Redirected Walking

This repository adds a basic implementation of redirected walking in VR applications. The system is expected to work best with Meta's `Meta SDK` package, though an `OpenXR` system should theoretically work if you mod some scripts.

This repository initially was a debug project for reading and interpreting the Quest Pro eye tracker.

## Required Add-Ons

* [**Min-Max-Slider**](https://github.com/SimpleDevs-Tools/SimpleMinMaxSlider): Inspector sliders for min-max variables. To install, do NOT follow the instructions provided in this repo. Instead, install the `Standalone.unitypackage` version that's provided among the releases. This is because the normal way to install this repo will produce a project that's safe for inspector runtime but is unsafe for standalone APKs.
* [**UnityUtils**](https://github.com/SimpleDevs-Tools/UnityUtils): Helper scripts. Simply clone this repo into the `Assets/` directory.

## Redirected Walking

To implement redirected walking, I recommend the following setup:

1. Create a "calibration" scene where you place spatial anchors, check your eye-tracking (if available), and confirm your boundary space. The scene should contain a `SpatialManager` script that should run when the scene first starts; this is a singleton that is maintained across scenes and retains your spatial boundary data across them. A template scene `RedirectedWalking/Scenes/0.Calibration.unity` is provided as an example. 
2. The next scene to build (via `Build Settings`) should also contain a `SpatialManager` script as well as a Game Object with a `Redirector` component attached. The `Redirector` must have its reference variables set and settings modified to your liking.
3. You're given the choice to set up your gain methods that will contribute to redirected walking. Four gain methods are provided. Make sure to add these as components into your scene and reference them in your `Redirector`'s `gain components` list:
    1. Curvature Gain: `CurvatureGain.cs`
    2. Rotational Gain: `RotationGain.cs`
    3. Saccade Suppression Gain: `SaccadeGain.cs` (requires the Quest Pro)
    4. Manual Gain: `ManualGain.cs`
4. Place your virtual environment into a root Game Object. This is essential for redirection to work. Make sure to reference the root environment Game Object inside the `Redirector` component.

When you run the game, you should first load in your calibration scene, then use `SpatialManager.TransitionToScene(int scene_id)` (or any other scene transition script you've cooked up yourself) to transition into the next scene you want redirected walking to be applied to.

A debug scene for this is provided in two scenes: `RedirectedWalking/Scenes/0.Calibration.unity` and `RedirectedWalking/Scenes/1.RDW2.unity`. In your Unity build, you should first load into `0.Calibration`, then press button 3 ("X" on the left controller) to transition into `1.RDW2`.

This repository was tested in both the Quest 3 and Quest Pro. Saccade Gain is only available on the Quest Pro.

## Eye Tracking Tests

When tethered to the PC, the USB cable used is a USB 3 with a bandwidth of 2.6 Gbps

Data collected from trials is provided in `python/samples`. Two types of samples exist - one titled `Old`, another titled `New`. Use the `New` data!

The axes of experimentation:

1. Standalone + Plugged into Outlet, Standalone + Battery, and PCVR
2. Confidence Thresholds: `0.0`, `0.5`

In each trial, you'll see both left and right eye data. Each eye has 4 entries:

* **Control**: Not wearing the headset, just letting it sit still on the table with as little light interference as possible.
* **3**: Wearing the headset, but trying to minimize eye fluctuations as much as posisble
* **2 and 1**: Wearing the headset, being chaotic with gaze movements as possible.

Each trial lasts approximatley 1 minute. Try to cull starting from the end and working backwards.
