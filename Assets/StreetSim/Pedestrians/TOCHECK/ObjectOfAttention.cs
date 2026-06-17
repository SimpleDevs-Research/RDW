using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BehaviorSuggestion
{
    NONE,
    LOOKAT,
    GOTO,
    AVOIDEYECONTACT,
    FLEE
}
public enum SenseType
{
    VISUAL,
    AUDIO
}
public class ObjectOfAttention : MonoBehaviour
{
    public float attentionPriority;
    public BehaviorSuggestion behaviorSuggestion;
    public ObjectOfAttention behaviorSuggestionObject;
    public AnimationCurve priorityFalloff;
    public float maxDistance = -1;
    public SenseType senseType;

    public virtual float GetAttentionPriority(Transform theTransform)
    {
        float thePriority = attentionPriority;
        if (maxDistance != -1)
            thePriority = attentionPriority * priorityFalloff.Evaluate( Vector3.Distance(transform.position, theTransform.position) / maxDistance);
       
        return thePriority;
    }
    public virtual (BehaviorSuggestion, ObjectOfAttention) GetBehaviorSuggestion(Transform theTransform)
    {
        (BehaviorSuggestion, ObjectOfAttention) suggestion = (behaviorSuggestion, behaviorSuggestionObject);
        return suggestion;
    }

}
