using System.Reflection;
using Attributes.Source.Infrastructure.Inspector;
using UnityEditor;
using UnityEngine;

namespace Attributes.Source.Infrastructure.Inspector.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class ButtonAttributeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var targetType = target.GetType();
            var methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
                
                if (buttonAttribute != null)
                {
                    string buttonLabel = string.IsNullOrEmpty(buttonAttribute.Label) ? method.Name : buttonAttribute.Label;

                    if (GUILayout.Button(buttonLabel))
                    {
                        Debug.Log(method.Name);
                        method.Invoke(target, null);
                    }
                }
            }
        }
    }
}