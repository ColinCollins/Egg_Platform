using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    public class OutputManagerWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset tree;

        // Main Label
        private ListView outputsList;
        private VisualElement masterOutputContainer;
        private VisualElement outputsListContainer;
        private VisualElement mainLabel;
        private Button openCreateLabelButton;
        private Button refreshButton;
        private Button mainOpenAudioMixerButton;
        
        // Create Label
        private Button backButton;
        private Button openAudioMixerButton;
        private Button createOutputButton;
        private VisualElement createOutputLabel;
        private VisualElement createGroupImage;
        private VisualElement renameGroupImage;
        private VisualElement exposeVolumeImage;
        private VisualElement locateExposedParamImage;
        private VisualElement renameExposedParamImage;
        
        private enum WindowLabel
        {
            Main,
            CreateOutput
        }
        
        [MenuItem("Tools/Melenitas Dev/Sounds Good/Output Manager", false, 52)]
        public static void ShowWindow ()
        {
            var window = GetWindow(typeof(OutputManagerWindow));
            window.titleContent = new GUIContent("Output Manager");
        }

        void CreateGUI ()
        {
            tree.CloneTree(rootVisualElement);

            openCreateLabelButton = rootVisualElement.Q<Button>("OpenCreateLabelButton");
            refreshButton = rootVisualElement.Q<Button>("RefreshButton");
            mainOpenAudioMixerButton = rootVisualElement.Q<Button>("MainOpenAudioMixerButton");
            masterOutputContainer = rootVisualElement.Q<VisualElement>("MasterOutputContainer");
            outputsList = rootVisualElement.Q<ListView>("OutputsList");
            outputsListContainer = outputsList.Q<VisualElement>("unity-content-container");
            backButton = rootVisualElement.Q<Button>("BackButton");
            openAudioMixerButton = rootVisualElement.Q<Button>("OpenAudioMixerButton");
            createOutputButton = rootVisualElement.Q<Button>("CreateOutputButton");
            mainLabel = rootVisualElement.Q<VisualElement>("MainLabel");
            createOutputLabel = rootVisualElement.Q<VisualElement>("CreateOutputLabel");
            createGroupImage = rootVisualElement.Q<VisualElement>("CreateGroupImage");
            renameGroupImage = rootVisualElement.Q<VisualElement>("RenameGroupImage");
            exposeVolumeImage = rootVisualElement.Q<VisualElement>("ExposeVolumeImage");
            locateExposedParamImage = rootVisualElement.Q<VisualElement>("LocateExposedParamImage");
            renameExposedParamImage = rootVisualElement.Q<VisualElement>("RenameExposedParamImage");
            rootVisualElement.Q<ListView>("InstructionsList").Q<VisualElement>("unity-content-container")
                .Add(rootVisualElement.Q<VisualElement>("Instructions"));
            
            RegisterEvents();
            
            ChangeLabel(WindowLabel.Main);
        }
        
        void OnDisable ()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        private void RegisterEvents ()
        {
            // Buttons
            openCreateLabelButton.clicked += OpenCreateLabel;
            refreshButton.clicked += CreateNewOutput;
            backButton.clicked += () => ChangeLabel(WindowLabel.Main);
            mainOpenAudioMixerButton.clicked += OpenAudioMixer;
            openAudioMixerButton.clicked += OpenAudioMixer;
            createOutputButton.clicked += CreateNewOutput;
            
            // Popup images
            createGroupImage.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button == 0) ImagePopupWindow.Show(AssetLocator.Instance.CreateGroupImage);
            });
            renameGroupImage.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button == 0) ImagePopupWindow.Show(AssetLocator.Instance.RenameGroupImage);
            });
            exposeVolumeImage.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button == 0) ImagePopupWindow.Show(AssetLocator.Instance.ExposeVolumeImage);
            });
            locateExposedParamImage.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button == 0) ImagePopupWindow.Show(AssetLocator.Instance.LocateExposedParamImage);
            });
            renameExposedParamImage.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button == 0) ImagePopupWindow.Show(AssetLocator.Instance.RenameExposedParamImage);
            });
            
            // Play Mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        private void OnPlayModeStateChanged (PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                openCreateLabelButton.SetEnabled(false);
                refreshButton.SetEnabled(false);
                mainOpenAudioMixerButton.SetEnabled(false);
                openAudioMixerButton.SetEnabled(false);
                createOutputButton.SetEnabled(false);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                openCreateLabelButton.SetEnabled(true);
                refreshButton.SetEnabled(true);
                mainOpenAudioMixerButton.SetEnabled(true);
                openAudioMixerButton.SetEnabled(true);
                createOutputButton.SetEnabled(true);
            }
        }

        private void DrawOutputs ()
        {
            outputsListContainer.hierarchy.Clear();
            masterOutputContainer.hierarchy.Clear();
            
            var outputCollection = AssetLocator.Instance.OutputDataCollection;
            int i = 0;
            foreach (OutputData outputData in outputCollection.Outputs)
            {
                bool exposed = outputData.Output.audioMixer
                    .GetFloat(outputData.Name.Replace(" ", ""), out float db);

                float volume = SoundsGoodManager.GetSavedOutputVolume(outputData.Name);
                
                var outputTemplate = new OutputTemplate(outputData.Name, exposed ? volume : -1, ChangeOutputVolume);
                if (i == 0)
                {
                    masterOutputContainer.Add(outputTemplate);
                    outputTemplate.SetBoldName();
                }
                else outputsListContainer.Add(outputTemplate);
                i++;
            }
        }
        
        private void ChangeOutputVolume (string outputName, float volume)
        {
            SoundsGoodManager.ChangeOutputVolume(outputName, volume);
        }

        private void ChangeLabel (WindowLabel windowLabel)
        {
            mainLabel.style.display = 
                windowLabel == WindowLabel.Main ? DisplayStyle.Flex : DisplayStyle.None;
            createOutputLabel.style.display =
                windowLabel == WindowLabel.CreateOutput ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (windowLabel == WindowLabel.Main) DrawOutputs();
        }
        
        private void OpenCreateLabel ()
        {
            ChangeLabel(WindowLabel.CreateOutput);
        }
        
        private void OpenAudioMixer ()
        {
            AudioMixer mixer = AssetLocator.Instance.MasterAudioMixer;
            
            if (mixer == null)
            {
                Debug.LogWarning("AudioMixer no asignado.");
                return;
            }
            
            EditorApplication.ExecuteMenuItem("Window/Audio/Audio Mixer");
            
            Selection.activeObject = mixer;
            
            EditorGUIUtility.PingObject(mixer);
        }

        private void CreateNewOutput ()
        {
            EditorHelper.ReloadOutputsDatabase();
            ChangeLabel(WindowLabel.Main);
        }
    }
}
