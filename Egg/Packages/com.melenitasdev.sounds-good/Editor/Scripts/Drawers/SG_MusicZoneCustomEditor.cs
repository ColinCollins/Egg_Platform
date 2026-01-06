#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MelenitasDev.SoundsGood.Editor
{
    public partial class SG_MusicZoneCustomEditor // Fields
    {
        private GUIStyle headerStyle;
        private GUIStyle foldoutStyle;
        private GUIStyle orangeLabelStyle;
        private GUIStyle lightOrangeLabelStyle;
        private GUIStyle darkOrangeLabelStyle;
        private GUIStyle lightOrangeBoxStyle;

        private Color orange = new Color(0.992f, 0.694f, 0.012f);
        private Color lightOrange = new Color(1, 0.953f, 0.847f);
        private Color darkOrange = new Color(0.984f, 0.482f, 0);

        private static bool showShapeLabel;
        private static bool showMusicLabel;
    }

    [CustomEditor(typeof(SG_MusicZone))]
    public partial class SG_MusicZoneCustomEditor : UnityEditor.Editor
    {
        void OnEnable ()
        {
            headerStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = orange
                }
            };
            foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                normal =
                {
                    textColor = orange
                }
            };
            orangeLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = orange
                }
            };
            lightOrangeLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = lightOrange
                }
            };
            darkOrangeLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = darkOrange
                }
            };
            lightOrangeBoxStyle = new GUIStyle("box")
            {
                normal =
                {
                    background = MakeTexture(1,1, lightOrange * new Color(1,1,1, 0.2f))
                }
            };
        }

        public override void OnInspectorGUI()
        {
            SG_MusicZone musicZone = (SG_MusicZone)target;

            EditorGUILayout.BeginHorizontal(lightOrangeBoxStyle);
            GUILayout.Space(15);
            showMusicLabel = EditorGUILayout.Foldout(showMusicLabel, "  Music", foldoutStyle);
            EditorGUILayout.EndHorizontal();
            if (showMusicLabel)
            {
                EditorGUILayout.BeginVertical(lightOrangeBoxStyle);
                EditorGUI.indentLevel++;
                MusicLabel(musicZone);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.BeginHorizontal(lightOrangeBoxStyle);
            GUILayout.Space(15);
            showShapeLabel = EditorGUILayout.Foldout(showShapeLabel, "  Shape", foldoutStyle);
            EditorGUILayout.EndHorizontal();
            if (showShapeLabel)
            {
                EditorGUILayout.BeginVertical(lightOrangeBoxStyle);
                EditorGUI.indentLevel++;
                ShapeLabel(musicZone);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(musicZone);
            }
        }
    }
    
    public partial class SG_MusicZoneCustomEditor // Private Methods
    {
        private void ShapeLabel (SG_MusicZone musicZone)
        {
            GUILayout.Space(3);
            GUILayout.Label("  Zone Shape", headerStyle);
            GUILayout.Space(3);

            musicZone.zoneShape = (SG_MusicZone.Shape)EditorGUILayout.EnumPopup("Shape", musicZone.zoneShape);

            GUILayout.Space(5);
            musicZone.useScaleAsZoneSize = EditorGUILayout.Toggle("Use Transform Scale", musicZone.useScaleAsZoneSize);
            musicZone.drawWireframe = EditorGUILayout.Toggle("Draw Wireframe", musicZone.drawWireframe);
            GUILayout.Space(3);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(3);
            if (musicZone.zoneShape == SG_MusicZone.Shape.Box)
            {
                GUILayout.Label("Box Size", headerStyle);
                GUILayout.Space(3);

                if (!musicZone.useScaleAsZoneSize)
                {
                    musicZone.width = EditorGUILayout.FloatField("Width", musicZone.width);
                    musicZone.width = musicZone.width < 0 ? 0 : musicZone.width;
                    musicZone.height = EditorGUILayout.FloatField("Height", musicZone.height);
                    musicZone.height = musicZone.height < 0 ? 0 : musicZone.height;
                    musicZone.depth = EditorGUILayout.FloatField("Depth", musicZone.depth);
                    musicZone.depth = musicZone.depth < 0 ? 0 : musicZone.depth;
                }
                else
                {
                    GUILayout.Space(5);
                    GUILayout.Label("     Using transform scale to resize zone", lightOrangeLabelStyle);
                    GUILayout.Space(5);
                }
                musicZone.extraBoxSizeFade = EditorGUILayout.FloatField("Fade Area", musicZone.extraBoxSizeFade);
                musicZone.extraBoxSizeFade = musicZone.extraBoxSizeFade < 0.05f ? 0.05f : musicZone.extraBoxSizeFade;
            }
            else if (musicZone.zoneShape == SG_MusicZone.Shape.Sphere)
            {
                GUILayout.Label("Sphere Size", headerStyle);
                GUILayout.Space(3);

                if (!musicZone.useScaleAsZoneSize)
                {
                    musicZone.radius = EditorGUILayout.FloatField("Radius", musicZone.radius);
                    musicZone.radius = musicZone.radius < 0 ? 0 : musicZone.radius;
                }
                else
                {
                    GUILayout.Space(3);
                    GUILayout.Label("     Using transform scale to resize zone", lightOrangeLabelStyle);
                    GUILayout.Space(3);
                }
                musicZone.extraRadiusFade = EditorGUILayout.FloatField("Fade Radius", musicZone.extraRadiusFade);
                musicZone.extraRadiusFade = musicZone.extraRadiusFade < 0.05f ? 0.05f : musicZone.extraRadiusFade;
            }

            musicZone.areaColor = EditorGUILayout.ColorField("Area Color", musicZone.areaColor);
            musicZone.fadeColor = EditorGUILayout.ColorField("Fade Color", musicZone.fadeColor);
            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void MusicLabel (SG_MusicZone musicZone)
        {
            GUILayout.Space(3);
            GUILayout.Label("  Player Mode", headerStyle);
            GUILayout.Space(3);

            musicZone.playerMode = (SG_MusicZone.PlayerMode)EditorGUILayout.EnumPopup("Mode", musicZone.playerMode);
            
            GUILayout.Space(5);
            GUILayout.Label("  Tracks", headerStyle);
            GUILayout.Space(3);
            
            switch (musicZone.playerMode)
            {
                case SG_MusicZone.PlayerMode.Music:
                default:
                {
                    if (musicZone.tracks.Count == 0) musicZone.tracks.Add(default);
                    musicZone.tracks[0] = EnumPopupHelper.TrackPopup("Track", musicZone.tracks[0]);
                    GUILayout.Space(3);
                    musicZone.volume = Mathf.Clamp(EditorGUILayout.FloatField("Volume" , musicZone.volume), 0, 1);
                    break;
                }
                case SG_MusicZone.PlayerMode.Playlist:
                {
                    if (musicZone.tracks.Count == 0) musicZone.tracks.Add(default);
                    EditorGUILayout.BeginVertical("box");
                    GUILayout.Space(5);
                    GUILayout.Label("     Add playlist tracks in order", lightOrangeLabelStyle);
                    GUILayout.Space(5);
                    for (int i = 0; i < musicZone.tracks.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        musicZone.tracks[i] = EnumPopupHelper.TrackPopup("", musicZone.tracks[i]);
                        if (musicZone.tracks.Count > 1)
                        {
                            GUILayout.Space(5);
                            if (GUILayout.Button("X", GUILayout.Width(20)))
                            {
                                musicZone.tracks.RemoveAt(i);
                                i--;
                            }
                            GUILayout.Space(5);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    GUILayout.Space(3);
                    if (GUILayout.Button("Add"))
                    {
                        musicZone.tracks.Add(default);
                    }
                    serializedObject.ApplyModifiedProperties();
                    GUILayout.Space(2);
                    EditorGUILayout.EndVertical();
                    musicZone.volume = Mathf.Clamp(EditorGUILayout.FloatField("Volume" , musicZone.volume), 0, 1);
                    break;
                }
                case SG_MusicZone.PlayerMode.DynamicMusic:
                {
                    if (musicZone.dynamicTracks.Count == 0) musicZone.dynamicTracks.Add(new SG_MusicZone.TrackInfo());
                    EditorGUILayout.BeginVertical("box");
                    GUILayout.Space(5);
                    GUILayout.Label("     Add the different tracks of the Dynamic Music", lightOrangeLabelStyle);
                    GUILayout.Space(5);
                    for (int i = 0; i < musicZone.dynamicTracks.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        var musicZoneDynamicTrack = musicZone.dynamicTracks[i];
                        musicZoneDynamicTrack.track = EnumPopupHelper.TrackPopup("", musicZoneDynamicTrack.track);
                        musicZoneDynamicTrack.volume = Mathf.Clamp(EditorGUILayout.FloatField(musicZoneDynamicTrack.volume), 0, 1);
                        musicZone.dynamicTracks[i] = musicZoneDynamicTrack;
                        if (musicZone.dynamicTracks.Count > 1)
                        {
                            GUILayout.Space(5);
                            if (GUILayout.Button("X", GUILayout.Width(20)))
                            {
                                musicZone.dynamicTracks.RemoveAt(i);
                                i--;
                            }
                            GUILayout.Space(5);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    GUILayout.Space(3);
                    if (GUILayout.Button("Add"))
                    {
                        musicZone.dynamicTracks.Add(new SG_MusicZone.TrackInfo());
                    }
                    serializedObject.ApplyModifiedProperties();
                    GUILayout.Space(2);
                    EditorGUILayout.EndVertical();
                    break;
                }
            }
            musicZone.loop = EditorGUILayout.Toggle("Loop", musicZone.loop);
            musicZone.output = EnumPopupHelper.OutputPopup("Audio Output", musicZone.output);
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}
#endif