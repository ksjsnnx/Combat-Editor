using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCombatSystem.CombatEditor
{
    /// <summary>
    /// 战斗序列资源类，用于存储和管理战斗轨道及片段的数据
    /// </summary>
    [CreateAssetMenu(fileName = "CombatSequence", menuName = "Combat/Combat Sequence", order = 10)]
    public sealed class CombatSequenceAsset : ScriptableObject
    {
        [SerializeField] private float duration = 2f;
        [SerializeField] private float frameRate = 30f;
        [SerializeField] private List<CombatTrack> tracks = new List<CombatTrack>();

        // 序列总持续时间（最小0.25秒）
        public float Duration
        {
            get => Mathf.Max(0.25f, duration);
            set => duration = Mathf.Max(0.25f, value);
        }

        // 帧率（用于编辑器对齐，最小1帧）
        public float FrameRate
        {
            get => Mathf.Max(1f, frameRate);
            set => frameRate = Mathf.Max(1f, value);
        }

        // 轨道列表
        public List<CombatTrack> Tracks => tracks;

        /// <summary> 根据GUID获取轨道 </summary>
        public CombatTrack GetTrack(string guid)
        {
            return tracks.Find(track => track.guid == guid);
        }

        /// <summary> 根据轨道和片段GUID获取片段 </summary>
        public CombatClip GetClip(string trackGuid, string clipGuid)
        {
            CombatTrack track = GetTrack(trackGuid);
            return track == null ? null : track.clips.Find(clip => clip.guid == clipGuid);
        }

        /// <summary> 确保数据的有效性，包括初始化列表、分配GUID和排序片段 </summary>
        public void EnsureValid()
        {
            tracks ??= new List<CombatTrack>();

            foreach (CombatTrack track in tracks)
            {
                if (string.IsNullOrWhiteSpace(track.guid))
                {
                    track.guid = Guid.NewGuid().ToString("N");
                }

                if (track.clips == null)
                {
                    track.clips = new List<CombatClip>();
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (string.IsNullOrWhiteSpace(clip.guid))
                    {
                        clip.guid = Guid.NewGuid().ToString("N");
                    }

                    clip.duration = Mathf.Max(0.01f, clip.duration);
                    clip.startTime = Mathf.Max(0f, clip.startTime);
                }

                // 按开始时间对片段进行排序
                track.clips.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            }

            duration = Mathf.Max(0.25f, duration);
            frameRate = Mathf.Max(1f, frameRate);
        }

        /// <summary> 添加一个新轨道 </summary>
        public CombatTrack AddTrack(CombatTrackType trackType)
        {
            CombatTrack track = new CombatTrack
            {
                displayName = trackType + " Track",
                trackType = trackType,
                color = GetDefaultColor(trackType)
            };

            tracks.Add(track);
            EnsureValid();
            return track;
        }

        /// <summary> 在指定时间点向轨道添加一个新片段 </summary>
        public CombatClip AddClip(CombatTrack track, float startTime)
        {
            if (track == null)
            {
                return null;
            }

            CombatClip clip = new CombatClip
            {
                displayName = track.trackType + " Clip",
                startTime = Mathf.Clamp(startTime, 0f, Duration),
                duration = GetSuggestedDuration(track.trackType),
                color = track.color
            };

            track.clips.Add(clip);
            EnsureValid();
            return clip;
        }

        /// <summary> 获取不同轨道类型的默认显示颜色 </summary>
        public static Color GetDefaultColor(CombatTrackType trackType)
        {
            switch (trackType)
            {
                case CombatTrackType.Animation:
                    return new Color(0.31f, 0.62f, 0.92f, 1f);
                case CombatTrackType.Movement:
                    return new Color(0.25f, 0.80f, 0.45f, 1f);
                case CombatTrackType.Hitbox:
                    return new Color(0.93f, 0.34f, 0.28f, 1f);
                case CombatTrackType.Effect:
                    return new Color(1f, 0.62f, 0.18f, 1f);
                case CombatTrackType.Audio:
                    return new Color(0.65f, 0.47f, 0.95f, 1f);
                case CombatTrackType.Camera:
                    return new Color(0.95f, 0.82f, 0.31f, 1f);
                case CombatTrackType.Event:
                    return new Color(0.46f, 0.87f, 0.85f, 1f);
                default:
                    return Color.gray;
            }
        }

        /// <summary> 获取不同轨道类型的建议初始时长 </summary>
        public static float GetSuggestedDuration(CombatTrackType trackType)
        {
            switch (trackType)
            {
                case CombatTrackType.Animation:
                    return 0.6f;
                case CombatTrackType.Movement:
                    return 0.35f;
                case CombatTrackType.Hitbox:
                    return 0.15f;
                case CombatTrackType.Effect:
                    return 0.25f;
                case CombatTrackType.Audio:
                    return 0.5f;
                case CombatTrackType.Camera:
                    return 0.2f;
                case CombatTrackType.Event:
                    return 0.1f;
                default:
                    return 0.25f;
            }
        }

        private void OnValidate()
        {
            EnsureValid();
        }
    }
}
