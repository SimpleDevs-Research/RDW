using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class DebugReceiverCollider : MonoBehaviour
{

    public enum DebugType { Receiver, Trigger }
    public DebugType type;
    public bool makeAnnouncements = true;

    private void OnTriggerEnter(Collider other) {
        if (!makeAnnouncements) return;
        DebugReceiverCollider collider = other.GetComponent<DebugReceiverCollider>();
        if (collider != null) {
            switch(collider.type) {
                case DebugType.Receiver:
                    Debug.Log($"Received  Receiver: {other.gameObject.name}");
                    break;
                case DebugType.Trigger:
                    Debug.Log($"Received Trigger: {other.gameObject.name}");
                    break;
            }
        }
    }
}
