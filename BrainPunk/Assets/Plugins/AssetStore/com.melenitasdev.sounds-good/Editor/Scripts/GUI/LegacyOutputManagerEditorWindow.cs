#if UNITY_EDITOR
/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    public class LegacyOutputManagerEditorWindow : EditorWindow
    {
        private OutputDataCollection outputDataCollection;

        private Dictionary<OutputData, bool> lastRefreshedOutputsDict = new Dictionary<OutputData, bool>();
        private Vector2 scrollPosition;

        private GUIStyle redBoxStyle;
        private GUIStyle greenBoxStyle;

        // [MenuItem("Tools/Melenitas Dev/Sounds Good/Legacy/Output Manager", false, 101)]
        public static void ShowWindow ()
        {
            LegacyOutputManagerEditorWindow window = 
                (LegacyOutputManagerEditorWindow)GetWindow(typeof(LegacyOutputManagerEditorWindow));
            window.Show();
            window.titleContent = new GUIContent("Output Manager");
        }

        void OnEnable ()
        {
            string path = "Melenitas Dev/Sounds Good/Outputs";
            outputDataCollection = Resources.Load<OutputDataCollection>($"{path}/OutputCollection");
            LoadOutputs();
        }

        void OnGUI ()
        {
            redBoxStyle = new GUIStyle(GUI.skin.box);
            greenBoxStyle = new GUIStyle(GUI.skin.box);
            redBoxStyle.normal.background = MakeTex(2, 2, new Color(0.58f, 0.15f, 0.15f, 0.3f));
            greenBoxStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.58f, 0.15f, 0.3f));

            EditorGUILayout.Space(20);
            
            CenterText("OUTPUTS MANAGER", 25, EditorWindowSharedTools.orange);
            
            GUILayout.Space(5);
            
            CenterText("Manage your audio outputs", 11, EditorWindowSharedTools.lightOrange);
            
            PlayModeMessage();
            
            GUI.enabled = !Application.isPlaying;
            
            GUILayout.Space(15);
            
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            if (GUILayout.Button("Reload Database", GUILayout.Height(45)))
            {
                LoadOutputs();
            }
            if (GUILayout.Button("Check Exposed Volumes", GUILayout.Height(24)))
            {
                CheckExposedVolumes();
            }

            if (outputDataCollection == null) return;

            GUILayout.Space(15);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var outputData in lastRefreshedOutputsDict)
            {
                bool exposed = outputData.Value;

                EditorGUILayout.BeginVertical(exposed ? greenBoxStyle : redBoxStyle);
                CenterText(outputData.Key.Name, 15, exposed ? Color.green : Color.red);
                string exposedText = exposed
                    ? "Volume is exposed correctly!"
                    : "Error: Volume is not exposed";
                CenterText(exposedText, 11, exposed ? Color.green : Color.red);
                GUILayout.Space(3);
                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
            }
            EditorGUILayout.EndScrollView();
            EditorGUI.EndDisabledGroup();

            EditorWindowSharedTools.LogoBanner();
        }

        private void CheckExposedVolumes ()
        {
            lastRefreshedOutputsDict.Clear();
            foreach (OutputData outputData in outputDataCollection.Outputs)
            {
                bool exposed = outputData.Output.audioMixer.GetFloat(outputData.Name.Replace(" ", ""), out float value);
                lastRefreshedOutputsDict.Add(outputData, exposed);
            }
        }
        
        private void LoadOutputs()
        {
            outputDataCollection = AssetLocator.Instance.OutputDataCollection;
            
            EditorHelper.ReloadOutputsDatabase();
            CheckExposedVolumes();
        }

        private void PlayModeMessage ()
        {
            if (!Application.isPlaying) return;
            EditorGUILayout.Space(10);
            CenterText("It cannot be used in Play mode", 13, new Color(0.65f, 0.25f, 0.25f));
        }
        
        private void CenterText (string text, int fontSize, Color color)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = color;
            style.focused.textColor = color;
            style.hover.textColor = color;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = fontSize;
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, style);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        private Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = color;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
#endif