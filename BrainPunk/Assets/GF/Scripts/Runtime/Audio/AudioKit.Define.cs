namespace GF
{
    public partial class AudioKit
    {
        private const float DefaultBGMVolume = 1f;
        private const float DefaultVoiceVolume = 1f;
        private const float DefaultUIVolume = 1f;
        private const float DefaultSceneVolume = 1f;
        private static int BGMMaxCount = 1;
        private static int VoiceMaxCount = 5;
        private static int UIMaxCount = 5;
        private static int SceneMaxCount = 5;

        private const string BGMVolumeKey = "BGM_VOLUME";
        private const string VoiceVolumeKey = "VOICE_VOLUME";
        private const string UIVolumeKey = "UI_VOLUME";
        private const string SceneVolumeKey = "SCENE_VOLUME";

        private const string BGMMuteKey = "BGM_MUTE";
        private const string VoiceMuteKey = "VOICE_MUTE";
        private const string UIMuteKey = "UI_MUTE";
        private const string SceneMuteKey = "SCENE_MUTE";
    }
}