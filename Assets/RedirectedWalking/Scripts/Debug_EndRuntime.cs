using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Debug_EndRuntime : MonoBehaviour
{
    public static Debug_EndRuntime current;
    public float waitSec = 60f;
    public bool initialize_at_start = true;
    private bool initialized = false;

    private void Awake()
    {
        current = this;
    }
    
    void Start()
    {
        if (initialize_at_start) StartCoroutine(Termination());
    }

    public void Initialize()
    {
        if (!initialized) StartCoroutine(Termination());
    }

    IEnumerator Termination()
    {
        initialized = true;
        yield return new WaitForSeconds(waitSec);
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #elif UNITY_WEBPLAYER
        Application.OpenURL(webplayerQuitURL);
        #else
        Application.Quit();
        #endif
    }
}
