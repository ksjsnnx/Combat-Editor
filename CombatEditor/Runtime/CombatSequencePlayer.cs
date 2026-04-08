using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCombatSystem.CombatEditor
{
    /// <summary>
    /// 战斗序列播放器，负责在运行时执行战斗序列资源中的动作和事件
    /// </summary>
    public sealed class CombatSequencePlayer : MonoBehaviour
    {
        [SerializeField] private CombatSequenceAsset sequence;
        [SerializeField] private CombatSequencePreviewBindings previewBindings;
        [SerializeField] private bool playOnStart;
        [SerializeField] private bool loop;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private bool logTriggeredClips = true;

        private readonly HashSet<string> activeClipIds = new HashSet<string>();
        private readonly List<GameObject> spawnedEffects = new List<GameObject>();
        private float currentTime;
        private Vector3 baseMovementPosition;
        private bool isPlaying;

        // 片段开始执行时触发的事件
        public event Action<CombatTrack, CombatClip> ClipStarted;
        // 片段执行结束时触发的事件
        public event Action<CombatTrack, CombatClip> ClipFinished;
        // 触发游戏逻辑事件时触发的事件
        public event Action<CombatClip> GameplayEventFired;

        // 当前关联的战斗序列资源
        public CombatSequenceAsset Sequence => sequence;
        // 当前播放时间（秒）
        public float CurrentTime => currentTime;
        // 是否正在播放中
        public bool IsPlaying => isPlaying;

        private void Start()
        {
            if (playOnStart && sequence != null)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!isPlaying || sequence == null)
            {
                return;
            }

            float previousTime = currentTime;
            currentTime += Time.deltaTime * Mathf.Max(0f, timeScale);

            // 检查序列是否结束
            if (currentTime >= sequence.Duration)
            {
                Evaluate(previousTime, sequence.Duration);

                if (loop)
                {
                    StopInternal(false);
                    currentTime = 0f;
                    isPlaying = true;
                    Evaluate(0f, 0f);
                    return;
                }

                StopInternal(true);
                return;
            }

            // 评估并更新当前时间的片段状态
            Evaluate(previousTime, currentTime);
        }

        /// <summary> 开始播放战斗序列 </summary>
        public void Play()
        {
            if (sequence == null)
            {
                return;
            }

            sequence.EnsureValid();
            currentTime = 0f;
            activeClipIds.Clear();
            baseMovementPosition = GetMovementRoot().position;
            ClearSpawnedEffects();
            isPlaying = true;
            Evaluate(0f, 0f);
            ApplyFrameState();
        }

        /// <summary> 停止播放战斗序列 </summary>
        public void Stop()
        {
            StopInternal(true);
        }

        /// <summary> 设置当前播放时间（手动跳转） </summary>
        public void SetTime(float time)
        {
            if (sequence == null)
            {
                currentTime = 0f;
                activeClipIds.Clear();
                return;
            }

            sequence.EnsureValid();
            currentTime = Mathf.Clamp(time, 0f, sequence.Duration);
            activeClipIds.Clear();
            Evaluate(currentTime, currentTime);
            ApplyFrameState();
        }

        /// <summary> 内部停止逻辑 </summary>
        private void StopInternal(bool notifyFinished)
        {
            if (notifyFinished && sequence != null)
            {
                NotifyFinishedActiveClips();
            }

            isPlaying = false;
            activeClipIds.Clear();
            ResetMovementRoot();
            ClearSpawnedEffects();
        }

        /// <summary> 通知所有当前活动的片段已结束 </summary>
        private void NotifyFinishedActiveClips()
        {
            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip != null && activeClipIds.Contains(clip.guid))
                    {
                        ClipFinished?.Invoke(track, clip);
                    }
                }
            }
        }

        /// <summary> 评估时间变化，触发片段的开始和结束 </summary>
        private void Evaluate(float previousTime, float newTime)
        {
            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    bool wasActive = activeClipIds.Contains(clip.guid);
                    bool isActive = clip.startTime <= newTime && clip.EndTime >= newTime;
                    bool startsNow = clip.startTime > previousTime && clip.startTime <= newTime;
                    bool firstFrame = Mathf.Approximately(previousTime, 0f) &&
                        Mathf.Approximately(newTime, 0f) &&
                        Mathf.Approximately(clip.startTime, 0f);

                    // 检查片段是否应开始
                    if (!wasActive && (isActive || startsNow || firstFrame))
                    {
                        activeClipIds.Add(clip.guid);
                        ClipStarted?.Invoke(track, clip);

                        if (logTriggeredClips)
                        {
                            Debug.Log($"[CombatSequence] Start {track.trackType} -> {clip.displayName}", this);
                        }

                        HandleClipStarted(track, clip);
                    }

                    // 检查片段是否应结束
                    if (wasActive && clip.EndTime < newTime)
                    {
                        activeClipIds.Remove(clip.guid);
                        ClipFinished?.Invoke(track, clip);

                        if (logTriggeredClips)
                        {
                            Debug.Log($"[CombatSequence] End {track.trackType} -> {clip.displayName}", this);
                        }
                    }
                }
            }

            ApplyFrameState();
        }

        /// <summary> 处理片段开始时的具体逻辑 </summary>
        private void HandleClipStarted(CombatTrack track, CombatClip clip)
        {
            switch (track.trackType)
            {
                case CombatTrackType.Animation:
                    PlayAnimationClip(clip);
                    break;
                case CombatTrackType.Effect:
                    SpawnEffect(clip);
                    break;
                case CombatTrackType.Audio:
                    PlayAudio(clip);
                    break;
                case CombatTrackType.Event:
                    GameplayEventFired?.Invoke(clip);
                    break;
            }
        }

        /// <summary> 应用当前帧的状态（如位移、Gizmos更新等） </summary>
        private void ApplyFrameState()
        {
            if (sequence == null)
            {
                return;
            }

            ApplyMovement();
            UpdatePreviewGizmos();
        }

        /// <summary> 处理位移逻辑 </summary>
        private void ApplyMovement()
        {
            Transform movementRoot = GetMovementRoot();
            if (movementRoot == null)
            {
                return;
            }

            Vector3 worldOffset = Vector3.zero;
            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Movement || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null || currentTime < clip.startTime || currentTime > clip.EndTime)
                    {
                        continue;
                    }

                    // 计算当前片段在时间轴上的进度，并应用曲线权重
                    float progress = Mathf.InverseLerp(clip.startTime, clip.EndTime, currentTime);
                    float weight = clip.moveCurve == null ? progress : clip.moveCurve.Evaluate(progress);
                    worldOffset += GetOwnerRoot().TransformDirection(clip.moveOffset) * weight;
                }
            }

            movementRoot.position = baseMovementPosition + worldOffset;
        }

        /// <summary> 播放动画剪辑 </summary>
        private void PlayAnimationClip(CombatClip clip)
        {
            if (previewBindings == null || previewBindings.Animator == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(clip.animationState))
            {
                previewBindings.Animator.Play(clip.animationState, 0, 0f);
                previewBindings.Animator.speed = Mathf.Max(0.01f, clip.animationSpeed);
            }
        }

        /// <summary> 生成特效 </summary>
        private void SpawnEffect(CombatClip clip)
        {
            if (clip.effectPrefab == null)
            {
                return;
            }

            Transform effectRoot = previewBindings != null ? previewBindings.EffectRoot : transform;
            Vector3 position = GetOwnerRoot().TransformPoint(clip.effectOffset);
            Quaternion rotation = GetOwnerRoot().rotation;
            GameObject instance = Instantiate(clip.effectPrefab, position, rotation, effectRoot);
            instance.transform.localScale = clip.effectScale;
            spawnedEffects.Add(instance);
            // 确保特效在片段持续时间后被销毁
            Destroy(instance, Mathf.Max(clip.duration, 0.05f));
        }

        /// <summary> 播放音效 </summary>
        private void PlayAudio(CombatClip clip)
        {
            if (clip.audioClip == null || previewBindings == null || previewBindings.AudioSource == null)
            {
                return;
            }

            previewBindings.AudioSource.PlayOneShot(clip.audioClip, clip.audioVolume);
        }

        /// <summary> 重置根物体位移 </summary>
        private void ResetMovementRoot()
        {
            Transform movementRoot = GetMovementRoot();
            if (movementRoot != null)
            {
                movementRoot.position = baseMovementPosition;
            }
        }

        /// <summary> 清理所有生成的特效 </summary>
        private void ClearSpawnedEffects()
        {
            for (int i = spawnedEffects.Count - 1; i >= 0; i--)
            {
                if (spawnedEffects[i] != null)
                {
                    Destroy(spawnedEffects[i]);
                }
            }

            spawnedEffects.Clear();
        }

        /// <summary> 更新编辑器预览辅助线(Gizmos) </summary>
        private void UpdatePreviewGizmos()
        {
            if (previewBindings != null)
            {
                previewBindings.SetPreviewState(sequence, currentTime);
            }
        }

        private Transform GetOwnerRoot()
        {
            return previewBindings != null ? previewBindings.OwnerRoot : transform;
        }

        private Transform GetMovementRoot()
        {
            return previewBindings != null ? previewBindings.MovementRoot : transform;
        }
    }

}
