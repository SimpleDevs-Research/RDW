using UnityEditor;
using UnityEngine;

namespace StreetSim {
    [CustomEditor(typeof(PedestrianGenerator))]
    public class PedestrianGeneratorEditor : Editor
    {
        
        public override void OnInspectorGUI() {
            
            serializedObject.Update();
            
            // Exclude certain properties in this extension. In the example below, we are hiding "speed" an "damage" from SerializedObject.
            //  DrawPropertiesExcluding(
            //      serializedObject,
            //      "speed",
            //      "damage"
            //  );

            DrawPropertiesExcluding(
                serializedObject, 
                "spawn_orientation"
            );

            serializedObject.ApplyModifiedProperties();
        }
    }
}
