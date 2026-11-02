using UnityEditor;
using System;
using System.Linq;
using System.Reflection;

namespace MelenitasDev.SoundsGood.Editor
{
    internal static class EnumPopupHelper
    {
        private static string[] trackNames;
        private static Track[] trackValues;
        private static bool trackCached;
        
        private static string[] outputNames;
        private static Output[] outputValues;
        private static bool outputCached;
        
        internal static Track TrackPopup (string label, Track current)
        {
            CacheTrackValues();

            if (trackNames.Length == 0)
                return current;

            int currentIndex = Array.IndexOf(trackValues, current);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = EditorGUILayout.Popup(label, currentIndex, trackNames);
            if (nextIndex != currentIndex && nextIndex >= 0)
                return trackValues[nextIndex];

            return current;
        }
        
        internal static Output OutputPopup (string label, Output current)
        {
            CacheOutputValues();

            if (outputNames.Length == 0)
                return current;

            int currentIndex = Array.IndexOf(outputValues, current);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = EditorGUILayout.Popup(label, currentIndex, outputNames);
            if (nextIndex != currentIndex && nextIndex >= 0)
                return outputValues[nextIndex];

            return current;
        }
        
        private static void CacheTrackValues ()
        {
            if (trackCached) return;

            var fields = typeof(Track)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Track));

            trackNames = fields.Select(f => f.Name).ToArray();
            trackValues = fields.Select(f => (Track)f.GetValue(null)).ToArray();
            trackCached = true;
        }
        
        private static void CacheOutputValues ()
        {
            if (outputCached) return;

            var fields = typeof(Output)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Output));

            outputNames = fields.Select(f => f.Name).ToArray();
            outputValues = fields.Select(f => (Output)f.GetValue(null)).ToArray();
            outputCached = true;
        }
    }
}