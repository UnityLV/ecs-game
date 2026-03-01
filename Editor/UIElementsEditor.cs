// Editor/UIElementsEditor.cs (полная замена)
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIElements))]
public class UIElementsEditor : Editor
{
    private FieldInfo[] _staticFields;

    private FieldInfo[] GetFields()
    {
        if (_staticFields != null) return _staticFields;
        var all = typeof(UIElements).GetFields(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var list = new List<FieldInfo>();
        foreach (var f in all)
            if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                list.Add(f);
        _staticFields = list.ToArray();
        return _staticFields;
    }

    private UIElementsBinding GetOrCreateBinding()
    {
        var go = ((UIElements)target).gameObject;
        var b = go.GetComponent<UIElementsBinding>();
        if (b == null)
            b = Undo.AddComponent<UIElementsBinding>(go);
        return b;
    }

    private void OnEnable()
    {
        // при открытии инспектора — загружаем сохранённые значения
        var binding = ((UIElements)target).GetComponent<UIElementsBinding>();
        if (binding != null) binding.Apply();
    }

    public override void OnInspectorGUI()
    {
        var fields = GetFields();

        EditorGUILayout.LabelField("Static UI References", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        bool changed = false;

        foreach (var field in fields)
        {
            var current = field.GetValue(null) as UnityEngine.Object;

            EditorGUI.BeginChangeCheck();

            var newVal = EditorGUILayout.ObjectField(
                ObjectNames.NicifyVariableName(field.Name),
                current,
                field.FieldType,
                true);

            if (EditorGUI.EndChangeCheck())
            {
                field.SetValue(null, newVal);
                changed = true;
            }
        }

        if (changed)
        {
            var binding = GetOrCreateBinding();
            Undo.RecordObject(binding, "UIElements changed");
            binding.SaveFromStatic();
            EditorUtility.SetDirty(binding);
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Clear All"))
        {
            foreach (var field in fields)
                field.SetValue(null, null);

            var binding = GetOrCreateBinding();
            Undo.RecordObject(binding, "Clear UIElements");
            binding.SaveFromStatic();
            EditorUtility.SetDirty(binding);
        }
    }
}