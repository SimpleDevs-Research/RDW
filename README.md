# TestQuestProEyeTracker

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