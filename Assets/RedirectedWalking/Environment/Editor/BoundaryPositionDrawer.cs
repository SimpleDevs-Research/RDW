using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RDW {
    [CustomPropertyDrawer(typeof(Environment.BoundaryPosition))]
    public class BoundaryPositionDrawer : PropertyDrawer
    {

        // ===============================
        // Drawer-specific Properties
        // ===============================
        private const float GridSize = 120f;
        private const float LinePadding = 4f;
        private static float GridMargin = 20f;
        private static float GridAnchorClickRadius = 10f;

        // ===============================
        // Boundary Anchor Lookup
        // ===============================
        private static readonly Environment.BoundaryAnchor[,] AnchorGrid = 
        {
            {
                Environment.BoundaryAnchor.NorthWest,
                Environment.BoundaryAnchor.North,
                Environment.BoundaryAnchor.NorthEast
            },
            {
                Environment.BoundaryAnchor.West,
                Environment.BoundaryAnchor.Center,
                Environment.BoundaryAnchor.East
            },
            {
                Environment.BoundaryAnchor.SouthWest,
                Environment.BoundaryAnchor.South,
                Environment.BoundaryAnchor.SouthEast
            }
        };

        // ===============================
        // Override the property height by adding more height for our grid view
        // ===============================
        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            return
                EditorGUIUtility.singleLineHeight + // label
                GridMargin +
                GridSize +                        // grid
                GridMargin +
                EditorGUIUtility.singleLineHeight + // anchor enum
                LinePadding +
                EditorGUIUtility.singleLineHeight + // offset
                LinePadding;
        }
        
        // ===============================
        // ON GUI Overwrite
        // ===============================
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label )
        {
            // Begin this class's property in the inspector
            EditorGUI.BeginProperty(  
                position,
                label,
                property);
            
            // ===============================
            // Current State Handling
            // ===============================
            Event e = Event.current;
            SerializedProperty anchorProperty = property.FindPropertyRelative("anchor");
            SerializedProperty offsetProperty = property.FindPropertyRelative("offset");

            // ===============================
            // First line: the folding in-out of the drawer in the inspector
            // ===============================
            Rect labelRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            // ===============================
            // Grid View & Interaction
            // ===============================

            // ------------------
            // Defining the grid area view
            // ------------------
            Rect gridRect = new Rect(
                position.x + (position.width - GridSize) * 0.5f,
                labelRect.yMax + GridMargin,
                GridSize,
                GridSize);

            // ------------------
            // Draw the grid view, add interactions for each anchor
            // ------------------
            DrawAnchorGrid( gridRect, anchorProperty, e);

            // ===============================
            // Offset View & Interaction
            // ===============================
        
            // ------------------
            // Handle Mouse Events
            // ------------------
            bool editing = false;
            
            // First type: mouse down
            if (e.type == EventType.MouseDown && e.button == 0 && gridRect.Contains(e.mousePosition)) {
                editing = true;
                e.Use();
            }
            // Second type: Mouse hold
            if (editing && e.type == EventType.MouseDrag) {
                UpdateOffsetFromMouse(e, gridRect, anchorProperty, offsetProperty);
                property.serializedObject.ApplyModifiedProperties();
                e.Use();
            }
            // Third type: Mouse release
            if (editing && e.type == EventType.MouseUp) {
                editing = false;
                e.Use();
            }

            // Draw the offset
            DrawOffset( gridRect, anchorProperty, offsetProperty, e);

            /*
            // Start drawing the offset marker
            Environment.BoundaryAnchor currentAnchor = (Environment.BoundaryAnchor)anchorProperty.enumValueIndex;
            Vector2 anchorGUIPos = GetAnchorGUIPosition( currentAnchor, gridRect );
            Vector2 offset = offsetProperty.vector2Value;
            Vector2 offsetGUIPos = anchorGUIPos + new Vector2(
                offset.x * gridRect.width,
                offset.y * gridRect.height
            );
            Handles.BeginGUI();
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(
                new Vector3(gridRect.xMin, offsetGUIPos.y),
                new Vector3(gridRect.xMax, offsetGUIPos.y),
                4f
            );
            Handles.DrawDottedLine(
                new Vector3(offsetGUIPos.x, gridRect.yMin),
                new Vector3(offsetGUIPos.x, gridRect.yMax),
                4f
            );
            float crossSize = 6f;
            Handles.DrawLine(
                offsetGUIPos + Vector2.left * crossSize,
                offsetGUIPos + Vector2.right * crossSize
            );
            Handles.DrawLine(
                offsetGUIPos + Vector2.up * crossSize,
                offsetGUIPos + Vector2.down * crossSize
            );
            if (e.type == EventType.MouseDown && e.button == 0 && gridRect.Contains(e.mousePosition)) {
                editingOffset = true;
                e.Use();
            }
            if (editingOffset && e.type == EventType.MouseDrag) {
                UpdateOffsetFromMouse(e, gridRect, currentAnchor, offsetProperty);
                property.serializedObject.ApplyModifiedProperties();
                e.Use();
            }
            if (editingOffset && e.type == EventType.MouseUp) {
                editingOffset = false;
                e.Use();
            }
            Handles.EndGUI();
            */

            //Anchor enum field
            Rect anchorRect = new Rect(
                position.x,
                gridRect.yMax + GridMargin,
                position.width,
                EditorGUIUtility.singleLineHeight
            );
            EditorGUI.PropertyField(
                anchorRect,
                anchorProperty);
            
            // Offset field
            Rect offsetRect = new Rect(
                position.x,
                anchorRect.yMax + LinePadding,
                position.width,
                EditorGUIUtility.singleLineHeight
            );
            EditorGUI.PropertyField(
                offsetRect,
                offsetProperty);

            EditorGUI.EndProperty();
        }

        // ===============================
        // GRID + ANCHOR DRAWING
        // ===============================
        private void DrawAnchorGrid( Rect rect,  SerializedProperty anchorProperty, Event e ) {
            
            // Start Handles
            Handles.BeginGUI();

            // ------------------
            // Draw grid background and outline - non-interactable
            // ------------------
            Handles.DrawSolidRectangleWithOutline(
                rect,                               // Which rect do we want to draw
                new Color(0.18f, 0.18f, 0.18f),     // What's the background color?
                Color.white                         // What's the boundary color?
            );

            // ------------------
            // Draw each anchor and handle their interaction
            // ------------------
            foreach(Environment.BoundaryAnchor anchor in Enum.GetValues(typeof(Environment.BoundaryAnchor))) {
                
                // get the GUI position of the current anchor
                Vector2 guiPos = GetAnchorGUIPosition(anchor, rect);

                // One thing to check: is this a value selected by the user already?
                bool selected = (anchorProperty.enumValueIndex == (int)anchor);

                // Define color based on selected
                Handles.color = selected ? Color.cyan : Color.white;
                
                // Draw the anchor point
                Handles.DrawSolidDisc(guiPos, Vector3.forward, selected ? 6f : 4f);
                
                // GUI clickable anchor
                Rect clickRect = new Rect(
                    guiPos.x - GridAnchorClickRadius,
                    guiPos.y - GridAnchorClickRadius,
                    GridAnchorClickRadius * 2f,
                    GridAnchorClickRadius * 2f
                );

                // Mouse on click event. We `use` the event to prevent it from being used anywhere else.
                if (e.type == EventType.MouseDown && clickRect.Contains(e.mousePosition)) {
                    anchorProperty.enumValueIndex = (int)anchor;
                    e.Use();
                }
            }

            // End Handles
            Handles.EndGUI();
        }

        // ===============================
        // OFFSET DRAWING
        // ===============================
        private void DrawOffset( 
                Rect rect, 
                SerializedProperty anchorProperty, 
                SerializedProperty offsetProperty, 
                Event e 
        ) {

            // ------------------
            // Get Current OFfset value
            // ------------------
            // This is some value (0-1, 0-1). Its orientation is relative to world space.
            Vector2 offsetValue = offsetProperty.vector2Value;
            
            // We convert it to be relative to grid rect space
            Vector2 offsetPosition = new Vector2(offsetValue.x * rect.width, -(offsetValue.y * rect.height));

            // Get the anchor position. This one is also oriented so that it's relative to world space
            Environment.BoundaryAnchor anchor = (Environment.BoundaryAnchor)anchorProperty.enumValueIndex;
            Vector2 anchorPosition = GetAnchorGUIPosition(anchor, rect);

            // The Offset GUI position therefore is:
            Vector2 offsetGuiPos = anchorPosition + offsetPosition;

            // ------------------
            // Draw Offset via Handles
            // ------------------

            // Start Handles
            Handles.BeginGUI();
            Handles.color = Color.yellow;
            
            Handles.DrawDottedLine(
                new Vector3(rect.xMin, offsetGuiPos.y),
                new Vector3(rect.xMax, offsetGuiPos.y),
                2f
            );
            Handles.DrawDottedLine(
                new Vector3(offsetGuiPos.x, rect.yMin),
                new Vector3(offsetGuiPos.x, rect.yMax),
                2f
            );
            float crossSize = 6f;
            Handles.DrawLine(
                offsetGuiPos + Vector2.left * crossSize,
                offsetGuiPos + Vector2.right * crossSize
            );
            Handles.DrawLine(
                offsetGuiPos + Vector2.up * crossSize,
                offsetGuiPos + Vector2.down * crossSize
            );

            // End Handles
            Handles.EndGUI();
        }

        private void UpdateOffsetFromMouse(Event e, Rect rect, SerializedProperty anchorProperty, SerializedProperty offsetProperty) {
            
            // Calculate mouse position relative to bottom left
            Vector2 mouseGUIPos = e.mousePosition - rect.min;    // Not yet in (0:1, 0:1), and local to top left
            Vector2 mousePos = new Vector2(
                mouseGUIPos.x / rect.width,
                (rect.height - mouseGUIPos.y) / rect.height
            );  // Now relative to bottom left and normalized to 0:1 for each axis
            offsetProperty.vector2Value = mousePos;
            
            /*
            // Get anchor position            
            Vector2 anchorLocal = Environment.GetAnchorPosition(anchor, rect.width, rect.height);
            // Make relative to the local anchor
            Vector2 delta = mousePos - anchorLocal;

            // Set the property value
            offsetProperty.vector2Value = delta;
            */
       }

        private Vector2 GetAnchorGUIPosition(
            Environment.BoundaryAnchor anchor,
            Rect rect
        ) {
            // Raw anchor positions are defined (0,0)-(1,1). You can get this value from
            // a static function inside of `Environment`
            Vector2 anchorPos = Environment.GetAnchorPosition(anchor, Vector2.zero, rect.size);
            
            // We fed a `Vector2.zero` to `GetAnchorPosition` because we need to position it a bit differently.
            return new Vector2(
                rect.xMin + anchorPos.x,
                rect.yMin + (rect.height - anchorPos.y)
            );
        }


        /*
        private Vector2 GetAnchorGUIPosition(
                Environment.BoundaryAnchor anchor,
                Rect rect, 
                float margin = 0f
        ) {
            // Primitives
            float width = rect.width - margin*2f;
            float height = rect.height - margin*2f;
            float xMin = rect.xMin;
            float yMin = rect.yMin;
            // Position calculation
            Vector2 pos = Environment.GetAnchorPosition(anchor, width, height);
            pos += Vector2.one * margin;
            // flip because y-coords in GUI are opposite
            return new Vector2(xMin + pos.x, yMin + (rect.height - pos.y));
        }

        private void UpdateOffsetFromMouse(Event e, Rect rect, Environment.BoundaryAnchor anchor, SerializedProperty offsetProperty) {
            // Calculate mouse position relative to bottom left
            Vector2 mouseGUIPos = e.mousePosition - rect.min;    // Not yet in (0:1, 0:1), and local to top left
            Vector2 mousePos = new Vector2(
                mouseGUIPos.x / rect.width,
                (rect.height - mouseGUIPos.y) / rect.height
            );  // Now relative to bottom left and normalized to 0:1 for each axis
            offsetProperty.vector2Value = mousePos;
            
            // Get anchor position            
            Vector2 anchorLocal = Environment.GetAnchorPosition(anchor, rect.width, rect.height);
            // Make relative to the local anchor
            Vector2 delta = mousePos - anchorLocal;

            // Set the property value
            offsetProperty.vector2Value = delta;
       }
        */
    }
}