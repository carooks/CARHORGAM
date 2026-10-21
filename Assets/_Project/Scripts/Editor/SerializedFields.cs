using UnityEditor;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Sets private [SerializeField] values from editor code, exactly as if typed in the Inspector.
    /// Logs an error if a field name is wrong so a renamed field can't fail silently.
    /// </summary>
    public static class SerializedFields
    {
        public static void Set(Object target, string field, Object value) => Apply(target, field, p => p.objectReferenceValue = value);
        public static void SetFloat(Object target, string field, float value) => Apply(target, field, p => p.floatValue = value);
        public static void SetBool(Object target, string field, bool value) => Apply(target, field, p => p.boolValue = value);
        public static void SetEnum(Object target, string field, int value) => Apply(target, field, p => p.enumValueIndex = value);

        static void Apply(Object target, string field, System.Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Horror Game] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            assign(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
