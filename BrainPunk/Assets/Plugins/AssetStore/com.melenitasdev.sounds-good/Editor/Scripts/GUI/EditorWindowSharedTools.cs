#if UNITY_EDITOR
/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
using UnityEditor;
using UnityEngine;

namespace MelenitasDev.SoundsGood.Editor
{
    public class EditorWindowSharedTools : EditorWindow
    {
        private static Texture2D banner;
        private const float RESOLUTION_FACTOR = 0.02f;
        
        internal static readonly Color orange = new Color(0.992f, 0.694f, 0.012f);
        internal static readonly Color lightOrange = new Color(1, 0.953f, 0.847f);
        internal static readonly Color darkOrange = new Color(0.984f, 0.482f, 0);
        
        internal static void CenterText (string text, int fontSize, Color color)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = color;
            style.focused.textColor = color;
            style.hover.textColor = color;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = fontSize;
            style.alignment = TextAnchor.MiddleCenter;
            style.wordWrap = true;
            
            GUILayout.Label(text, style);
        }
        
        internal static void LogoBanner ()
        {
            if (banner == null)
            {
                banner = EditorGUIUtility
                    .Load("Assets/Plugins/Melenitas Dev/Sounds Good/Graphics/Sounds Good Logo.png") as Texture2D;
            }
            if (banner != null)
            {
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal("box");
                GUILayout.FlexibleSpace();
                Rect imageRect = GUILayoutUtility.GetRect(8000 * RESOLUTION_FACTOR, 3418 * RESOLUTION_FACTOR);
                GUI.DrawTexture(imageRect, banner);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }
    }
}
#endif