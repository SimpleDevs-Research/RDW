using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace StreetSim {

    [System.Serializable]
    public class TimerPackage {
        public float time = 0f;
        public int frame = 0;

        public TimerPackage() {}
        public TimerPackage(float t, int f) {
            time = t;
            frame = f;
        }
        public TimerPackage(TimerPackage tp) {
            time = tp.time;
            frame = tp.frame;
        }
        public void Update(float t, int f) {
            time = t;
            frame = f;
        }
        public void Update(TimerPackage end, TimerPackage start) {
            time = end.time - start.time;
            frame = end.frame - start.frame;
        } 
    }

    public class Timer : MonoBehaviour
    {

        public enum State { Stopped, Paused, Running }

        [Header("=== Events ===")]
        [Tooltip("When the timer is STARTED, call any event. Returns when the timer was started")]
        public UnityEvent<TimerPackage> onStarted;
        [Tooltip("When the timer is PAUSED, call any event. Returns when the timer is paused and the current state.")]
        public UnityEvent<TimerPackage, TimerPackage> onPaused;
        [Tooltip("When the timer is PLAYED (i.e. unpaused), call any event. Returns when the timer is played and the current state.")]
        public UnityEvent<TimerPackage, TimerPackage> onPlayed;
        [Tooltip("When the timer is STOPPED (i.e. ended without reaching the end duration), call any event. Returns when the timer is stopped and the final timestamps.")]
        public UnityEvent<TimerPackage, TimerPackage> onStopped;
        [Tooltip("When the timer is ENDED (i.e. ended after reaching the end duration), call any event. Returns when the timer ended and the final timestamps.")]
        public UnityEvent<TimerPackage, TimerPackage> onEnded;

        [Header("=== Settings ===")]
        [Min(0.1f), SerializeField, Tooltip("How long should the timer run, in seconds?")] 
        private float duration;

        private TimerPackage _startTime;
        private TimerPackage _endTime;
        private TimerPackage _passedTime;

        public TimerPackage startTime => _startTime;
        public TimerPackage endTime => _endTime;
        public TimerPackage passedTime => _passedTime;

        public State state = State.Stopped;
        public bool isRunning => state == State.Running;
        public bool isPaused => state == State.Paused;
        public bool isStopped => state == State.Stopped;

        // - forceRestart : bool = if the timer is already running, then you can force this timer to reset
        public bool StartTimer(bool forceRestart = false) {
            // Can't do anything if we aren't force restarting
            if (!forceRestart && !isStopped) return false; 

            // Reset values
            _startTime = new TimerPackage( Time.time, Time.frameCount );
            _endTime = new TimerPackage( _startTime );
            _passedTime = new TimerPackage();

            // Start the timer
            onStarted?.Invoke(startTime);
            state = State.Running;
            return true;
        }

        public bool PlayTimer() {
            if (isPaused) {     // A timer can only be unpaused if it is paused
                state = State.Running;
                onPlayed?.Invoke(endTime, passedTime);
                return true;
            }
            return false;
        }
        public bool PauseTimer() {
            if (isRunning) {            // A timer can only be paused if it is running.
                state = State.Paused;   // Set the state to `paused`
                onPaused?.Invoke(endTime, passedTime);    // Call events
                return true;            // Inform the caller that this was successful.
            }
            return false;               // Inform the caller that this was unsuccessful.
        }

        public bool StopTimer() {
            if (isStopped) return false;   // A timer can only be stopped if the timer is running or paused
            
            // Set some values
            _endTime.Update(Time.time, Time.frameCount);
            _passedTime.Update(_endTime, _startTime);

            // Invoke events and inform caller of state change
            onStopped?.Invoke(endTime, passedTime);
            state = State.Stopped;
            return true;
        }

        // This is a UNIQUE function. Only called by `Update()` when the timer reaches the end
        private void EndTimer() {            
            // Set some values
            state = State.Stopped;

            // We already yupdated `_endTime` and `_passedTime` in `Update()`, so no need to call them again
            // Invoke events and inform caller of state change
            onEnded?.Invoke(endTime, passedTime);
            state = State.Stopped;
        }

        private void Update() {
            // Don't do anything if we aren't running
            if (!isRunning) return;
            
            // Update
            _endTime.Update(Time.time, Time.frameCount);
            _passedTime.Update(_endTime, _startTime);

            // Check: did we pass our intended duration?
            if (_passedTime.time >= duration) EndTimer();
        }

        public void DebugStarted() { Debug.Log("Timer Started!"); }
        public void DebugPaused() { Debug.Log("Timer Paused!"); }
        public void DebugPlayed() { Debug.Log("Timer Played!"); }
        public void DebugStopped() { Debug.Log("Timer Stopped!"); }
        public void DebugEnded() { Debug.Log("Timer Ended!"); }
    }
}
