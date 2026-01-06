using System;
using System.Collections.Generic;
using UnityEngine;

namespace MelenitasDev.SoundsGood
{
    public partial class SG_MusicZone // Fields
    {
        private Transform playerCamera;

        private Music music;
        private Playlist playlist;
        private DynamicMusic dynamicMusic;

        private Vector3 closestPoint;
        private Vector3 closestFadePoint;
        private float maxDistanceBoxFade;

        private Vector3 boxSize;
        private Vector3 fadeBoxSize;
        private float fadeRadius;
        
        private bool playerJustExitFadeZone;
        private bool playerJustEnterMusicZone;
    }

    public partial class SG_MusicZone // Properties
    {
        // Shape Label
        public Shape zoneShape;

        public bool useScaleAsZoneSize = false;
        public bool drawWireframe = true;
        
        public float radius = 1f;
        public float extraRadiusFade = 1f;
        public float height = 1f;
        public float width = 1f;
        public float depth = 1f;
        public float extraBoxSizeFade = 1f;
        
        public Color areaColor = new Color(0.992f, 0.694f, 0.012f);
        public Color fadeColor = new Color(1, 0.953f, 0.847f);
        
        public enum Shape
        {
            Sphere,
            Box
        }
        
        // Music Label
        public PlayerMode playerMode = PlayerMode.Music;
        
        // Music & Playlist
        public List<Track> tracks = new List<Track>();
        public float volume = 1f;

        // Dynamic Music
        public List<TrackInfo> dynamicTracks = new List<TrackInfo>();
        
        public bool loop;
        public Output output = default;
        
        [Serializable]
        public class TrackInfo
        {
            public Track track = default;
            public float volume = 1f;
        }
        
        public enum PlayerMode
        {
            Music,
            Playlist,
            DynamicMusic
        }
    }

    public partial class SG_MusicZone : MonoBehaviour
    {
        void Awake ()
        {
            playerCamera = Camera.main.transform;
            
            boxSize = useScaleAsZoneSize ? transform.localScale : new Vector3(width, height, depth);
            fadeBoxSize = boxSize * (extraBoxSizeFade + 1);
            radius = useScaleAsZoneSize ? transform.localScale.x : radius;
            fadeRadius = radius * (extraRadiusFade + 1);
        }

        void Start ()
        {
            if (playerMode == PlayerMode.Music)
            {
                music = new Music(tracks[0]);
                music
                    .SetLoop(loop)
                    .SetVolume(volume)
                    .SetSpatialSound(false)
                    .SetPosition(transform.position)
                    .SetOutput(output)
                    .Play();
            }
            else if (playerMode == PlayerMode.Playlist)
            {
                playlist = new Playlist(tracks.ToArray());
                playlist
                    .SetLoop(loop)
                    .SetVolume(volume)
                    .SetSpatialSound(false)
                    .SetPosition(transform.position)
                    .SetOutput(output)
                    .Play();
            }
            else
            {
                Track[] tracks = new Track[dynamicTracks.Count];
                int i = 0;
                foreach (TrackInfo dynamicTrack in dynamicTracks)
                {
                    tracks[i] = dynamicTrack.track;
                    i++;
                }
                dynamicMusic = new DynamicMusic(tracks);
                dynamicMusic
                    .SetLoop(loop)
                    .SetSpatialSound(false)
                    .SetPosition(transform.position)
                    .SetOutput(output);
                foreach (TrackInfo dynamicTrack in dynamicTracks)
                {
                    dynamicMusic.SetTrackVolume(dynamicTrack.track, dynamicTrack.volume);
                }
                dynamicMusic.Play();
            }

            playerJustEnterMusicZone = true;
            MuteVolume();
        }
        
        void Update ()
        {
            if (zoneShape == Shape.Box)
            {
                HandleBoxVolume();
                return;
            }
            HandleSphereVolume();
        }

        void OnDrawGizmos ()
        {
            boxSize = useScaleAsZoneSize ? transform.localScale : new Vector3(width, height, depth);
            fadeBoxSize = boxSize * (extraBoxSizeFade + 1);
            radius = useScaleAsZoneSize ? transform.localScale.x : radius;
            fadeRadius = radius * (extraRadiusFade + 1);
            
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(closestPoint, 0.3f);
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(closestFadePoint, 0.3f);
            
            if (zoneShape == Shape.Box)
            {
                Gizmos.color = areaColor; ;
                if (drawWireframe) Gizmos.DrawWireCube(transform.position, boxSize);
                else Gizmos.DrawCube(transform.position, boxSize);
                if (extraBoxSizeFade <= 0) return;
                Gizmos.color = fadeColor;
                if (drawWireframe) Gizmos.DrawWireCube(transform.position, fadeBoxSize);
                else Gizmos.DrawCube(transform.position, fadeBoxSize);
                return;
            }

            Gizmos.color = areaColor;
            if (drawWireframe) Gizmos.DrawWireSphere(transform.position, radius);
            else Gizmos.DrawSphere(transform.position, radius);
            if (extraRadiusFade <= 0) return;
            Gizmos.color = fadeColor;
            if (drawWireframe) Gizmos.DrawWireSphere(transform.position, fadeRadius);
            else Gizmos.DrawSphere(transform.position, fadeRadius);
        }
    }

    public partial class SG_MusicZone // Private Methods
    {
        private void HandleBoxVolume ()
        {
            Vector3 cameraPosition = playerCamera.position;
            Vector3 boxCenter = transform.position;

            Vector3 halfSize = boxSize / 2;
            Vector3 boxMin = boxCenter - halfSize;
            Vector3 boxMax = boxCenter + halfSize;

            Vector3 fadeHalfSize = fadeBoxSize / 2;
            Vector3 fadeBoxMin = boxCenter - fadeHalfSize;
            Vector3 fadeBoxMax = boxCenter + fadeHalfSize;

            bool playerInsideBox = cameraPosition.x >= boxMin.x && cameraPosition.x <= boxMax.x &&
                                   cameraPosition.y >= boxMin.y && cameraPosition.y <= boxMax.y &&
                                   cameraPosition.z >= boxMin.z && cameraPosition.z <= boxMax.z;

            bool playerInsideFade = cameraPosition.x >= fadeBoxMin.x && cameraPosition.x <= fadeBoxMax.x &&
                                    cameraPosition.y >= fadeBoxMin.y && cameraPosition.y <= fadeBoxMax.y &&
                                    cameraPosition.z >= fadeBoxMin.z && cameraPosition.z <= fadeBoxMax.z;

            closestPoint = new Vector3(
                Mathf.Clamp(cameraPosition.x, boxMin.x, boxMax.x),
                Mathf.Clamp(cameraPosition.y, boxMin.y, boxMax.y),
                Mathf.Clamp(cameraPosition.z, boxMin.z, boxMax.z)
            );

            closestFadePoint = new Vector3(
                Mathf.Clamp(cameraPosition.x, fadeBoxMin.x, fadeBoxMax.x),
                Mathf.Clamp(cameraPosition.y, fadeBoxMin.y, fadeBoxMax.y),
                Mathf.Clamp(cameraPosition.z, fadeBoxMin.z, fadeBoxMax.z)
            );

            if (playerInsideBox)
            {
                if (playerJustEnterMusicZone)
                {
                    SetMaxVolume();
                    playerJustEnterMusicZone = false;
                }
                return;
            }

            if (!playerJustEnterMusicZone)
            {
                playerJustEnterMusicZone = true;
            }

            if (!playerInsideFade)
            {
                if (playerJustExitFadeZone)
                {
                    MuteVolume();
                    playerJustExitFadeZone = false;
                }
                
                maxDistanceBoxFade = Vector3.Distance(closestPoint, closestFadePoint);
            }
            else
            {
                if (maxDistanceBoxFade == 0)
                {
                    Vector3 directionToCamera = (closestPoint - playerCamera.position).normalized;
                    Vector3 relativeCameraPosition = closestPoint + (directionToCamera * (extraRadiusFade * 5));
                    maxDistanceBoxFade = Vector3.Distance(relativeCameraPosition, playerCamera.position);
                }
                ChangeVolumeInBoxFadeZone();

                if (!playerJustExitFadeZone)
                {
                    playerJustExitFadeZone = true;
                }
            }
        }

        private void HandleSphereVolume ()
        {
            Vector3 directionToCamera = (playerCamera.position - transform.position).normalized;
            closestFadePoint = transform.position + (directionToCamera * fadeRadius);
            closestPoint = transform.position + (directionToCamera * radius);
            
            float distanceToSphere = Vector3.Distance(closestPoint, playerCamera.position);
            float distanceToCenter = Vector3.Distance(transform.position, playerCamera.position);
            bool playerInsideFade = distanceToCenter < fadeRadius;
            bool playerInsideSphere = distanceToCenter < radius;

            if (playerInsideSphere)
            {
                if (playerJustEnterMusicZone)
                {
                    SetMaxVolume();
                    playerJustEnterMusicZone = false;
                }
                return;
            }
            
            if (!playerInsideFade)
            {
                if (playerJustExitFadeZone)
                {
                    MuteVolume();
                    playerJustEnterMusicZone = false;
                }
            }
            
            ChangeVolumeInSphereFadeZone(distanceToSphere);
            if (!playerJustEnterMusicZone)
            {
                playerJustEnterMusicZone = true;
            }
        }
        
        private void ChangeVolumeInBoxFadeZone ()
        {
            float currentDistance = Vector3.Distance(closestPoint, closestFadePoint);
            float targetVolume = currentDistance * volume / maxDistanceBoxFade;
            targetVolume = volume - targetVolume;
                    
            if (playerMode == PlayerMode.Music) music.ChangeVolume(targetVolume);
            else if (playerMode == PlayerMode.Playlist) playlist.ChangeVolume(targetVolume);
            else
            {
                foreach (TrackInfo dynamicTrack in dynamicTracks)
                {
                    targetVolume = currentDistance * dynamicTrack.volume / maxDistanceBoxFade;
                    targetVolume = dynamicTrack.volume - targetVolume;
                    dynamicMusic.ChangeTrackVolume(dynamicTrack.track, targetVolume);
                }
            }
        }
        
        private void ChangeVolumeInSphereFadeZone (float distanceToCenter)
        {
            float maxDistance = Vector3.Distance(closestFadePoint, closestPoint);
            float targetVolume = distanceToCenter * volume / maxDistance;
            targetVolume = volume - targetVolume;
                    
            if (playerMode == PlayerMode.Music) music.ChangeVolume(targetVolume);
            else if (playerMode == PlayerMode.Playlist) playlist.ChangeVolume(targetVolume);
            else
            {
                foreach (TrackInfo dynamicTrack in dynamicTracks)
                {
                    targetVolume = distanceToCenter * dynamicTrack.volume / maxDistance;
                    targetVolume = dynamicTrack.volume - targetVolume;
                    dynamicMusic.ChangeTrackVolume(dynamicTrack.track, targetVolume);
                }
            }
        }
        
        private void MuteVolume ()
        {
            float fadeOutTime = 0;
            if (zoneShape == Shape.Box)
            {
                float currentDistance = Vector3.Distance(closestPoint, closestFadePoint);
                if (currentDistance < maxDistanceBoxFade - 0.1f)
                {
                    print("Fade");
                    fadeOutTime = 0.5f;
                }
            }

            if (playerMode == PlayerMode.Music) music.ChangeVolume(0, fadeOutTime);
            else if (playerMode == PlayerMode.Playlist) playlist.ChangeVolume(0, fadeOutTime);
            else
            {
                foreach (TrackInfo dynamicTrack in dynamicTracks)
                {
                    dynamicMusic.ChangeTrackVolume(dynamicTrack.track, 0, fadeOutTime);
                }
            }
        }
        
        private void SetMaxVolume ()
        {
            if (playerMode == PlayerMode.Music) music.ChangeVolume(volume);
            else if (playerMode == PlayerMode.Playlist) playlist.ChangeVolume(volume);
            else
            {
                foreach (TrackInfo dynamicTrack in dynamicTracks)
                {
                    dynamicMusic.ChangeTrackVolume(dynamicTrack.track, dynamicTrack.volume);
                }
            }
        }
    }
}