using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RDW {
    public class Toggleable : MonoBehaviour
    {
        public void Toggle() { gameObject.SetActive(!gameObject.activeInHierarchy); }
    }
}

