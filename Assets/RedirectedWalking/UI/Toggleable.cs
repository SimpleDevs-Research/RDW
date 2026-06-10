using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Toggleable : MonoBehaviour
    {
        public void Toggle() { gameObject.SetActive(!gameObject.activeInHierarchy); }
        public void Toggle(bool t) { gameObject.SetActive(t); }
        public void Activate() { gameObject.SetActive(true); }
        public void Deactivate() { gameObject.SetActive(false); }
    }
}

