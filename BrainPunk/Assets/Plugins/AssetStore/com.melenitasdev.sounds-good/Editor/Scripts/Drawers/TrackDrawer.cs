using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MelenitasDev.SoundsGood.Editor
{
    [CustomPropertyDrawer(typeof(Track))]
    public class TrackDrawer : PropertyDrawer
    {
        private string[] names;
        private Track[] values;
        private bool cached;

        private void Cache ()
        {
            if (cached) return;

            var fields = typeof(Track)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Track));

            var nameList = fields.Select(f => f.Name).ToList();
            var valueList = fields.Select(f => (Track)f.GetValue(null)).ToList();

            if (nameList.Count == 0)
            {
                names = new[] { "-----" };
                values = new[] { new Track(string.Empty) };
            }
            else
            {
                names = nameList.ToArray();
                values = valueList.ToArray();
            }

            cached = true;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Cache();
            
            SerializedProperty stringProp = property.FindPropertyRelative("value");
            
            string current = stringProp.stringValue;
            int index = Array.IndexOf(values, new SFX(current));
            if (index < 0) index = 0;
            
            var choices = names.ToList();
            
            var popup = new PopupField<string>(choices, index)
            {
                label = property.displayName,
                style =
                {
                    flexGrow = 1
                }
            };

            popup.RegisterValueChangedCallback(evt =>
            {
                int newIdx = choices.IndexOf(evt.newValue);
                if (newIdx >= 0 && newIdx < values.Length)
                {
                    stringProp.stringValue = values[newIdx].ToString();
                    property.serializedObject.ApplyModifiedProperties();
                }
            });
            
            if (index != choices.IndexOf(popup.value))
            {
                stringProp.stringValue = values[index].ToString();
                property.serializedObject.ApplyModifiedProperties();
            }
            
            return popup;
        }
    }
}