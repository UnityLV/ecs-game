using System.Reflection;
using UnityEngine;

[ExecuteAlways]
public class UIElementsBinding : MonoBehaviour
{
    [SerializeField] private UnityEngine.Object[] _values;
    [SerializeField] private string[] _fieldNames;

    private static FieldInfo[] GetStaticObjectFields()
    {
        var all = typeof(UIElements).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var list = new System.Collections.Generic.List<FieldInfo>();
        foreach (var f in all)
            if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                list.Add(f);
        return list.ToArray();
    }

    private void OnEnable() => Apply();
    private void Awake() => Apply();

    public void Apply()
    {
        if (_fieldNames == null || _values == null) return;
        var fields = GetStaticObjectFields();
        for (int i = 0; i < _fieldNames.Length && i < _values.Length; i++)
        {
            foreach (var f in fields)
            {
                if (f.Name == _fieldNames[i])
                {
                    f.SetValue(null, _values[i]);
                    break;
                }
            }
        }
    }

    public void SaveFromStatic()
    {
        var fields = GetStaticObjectFields();
        _fieldNames = new string[fields.Length];
        _values = new UnityEngine.Object[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            _fieldNames[i] = fields[i].Name;
            _values[i] = fields[i].GetValue(null) as UnityEngine.Object;
        }
    }
}