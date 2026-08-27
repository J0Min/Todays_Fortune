using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VerletRope2D))]
public sealed class VerletRope2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (!GUILayout.Button("Rebuild Rope"))
            return;

        foreach (Object targetObject in targets)
        {
            VerletRope2D rope = (VerletRope2D)targetObject;
            rope.BuildRope();
            EditorUtility.SetDirty(rope);
        }
    }
}
