/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
using UnityEngine;

namespace MelenitasDev.SoundsGood.Domain
{
    public partial class SoundData // Serialized Fields
    {
        [SerializeField] private string tag;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private CompressionPreset compressionPreset;
        [SerializeField] private bool forceToMono;
    }
    
    public partial class SoundData // Properties
    {
        public string Tag { get => tag; set => tag = value; }
        public AudioClip[] Clips { get => clips; set => clips = value; }
        public CompressionPreset CompressionPreset { get => compressionPreset; set => compressionPreset = value; }
        public bool ForceToMono { get => forceToMono; set => forceToMono = value; }
    }
    
    [System.Serializable]
    public partial class SoundData // Public Methods
    {
        public SoundData (string tag, AudioClip[] clips, CompressionPreset compressionPreset, bool forceToMono)
        {
            this.tag = tag;
            this.clips = clips;
            this.compressionPreset = compressionPreset;
            this.forceToMono = forceToMono;
        }

        public AudioClip GetClip (int index)
        {
            if (index > clips.Length - 1)
            {
                Debug.LogWarning($"There's no audio clip in the index '{index}'. " +
                                 $"Audio '{tag}' has {clips.Length} audio clips attached.");
                index = -1;
            }
            
            if (index == -1)
            {
                return clips[Random.Range(0, clips.Length)];
            }

            return clips[index];
        }
    }
}
