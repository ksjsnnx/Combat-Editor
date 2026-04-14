using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NewCombatSystem.CombatEditor.Editor
{
    internal static class CombatSequenceEditorAudioPreview
    {
        private static CombatSequenceAsset lastSequence;
        private static float lastSampleTime;
        private static bool hasLastSample;
        private static AudioClip lastPreviewedAudioClip;
        private static int lastPreviewedAudioSample;

        public static void Sample(CombatSequenceAsset sequence, float time)
        {
            if (sequence == null || !EditorAudioUtil.IsAvailable)
            {
                return;
            }

            if (lastSequence != sequence)
            {
                Stop();
            }

            bool isContinuousPlayback = hasLastSample &&
                time >= lastSampleTime &&
                time - lastSampleTime <= 0.12f;

            if (isContinuousPlayback)
            {
                PreviewCrossedClipStarts(sequence, lastSampleTime, time);
            }
            else
            {
                PreviewActiveClipAtTime(sequence, time);
            }

            lastSequence = sequence;
            lastSampleTime = time;
            hasLastSample = true;
        }

        public static void Stop()
        {
            EditorAudioUtil.StopAllPreviewClips();
            lastSequence = null;
            hasLastSample = false;
            lastPreviewedAudioClip = null;
            lastPreviewedAudioSample = 0;
        }

        private static void PreviewCrossedClipStarts(CombatSequenceAsset sequence, float previousTime, float currentTime)
        {
            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Audio || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null || clip.audioClip == null)
                    {
                        continue;
                    }

                    if (clip.startTime > previousTime && clip.startTime <= currentTime)
                    {
                        PlayPreviewAudio(clip.audioClip, 0);
                    }
                }
            }
        }

        private static void PreviewActiveClipAtTime(CombatSequenceAsset sequence, float time)
        {
            CombatClip activeClip = GetActiveAudioClip(sequence, time);
            if (activeClip == null || activeClip.audioClip == null)
            {
                Stop();
                return;
            }

            int startSample = Mathf.Clamp(
                Mathf.RoundToInt((time - activeClip.startTime) * activeClip.audioClip.frequency),
                0,
                Mathf.Max(0, activeClip.audioClip.samples - 1));

            int sampleThreshold = Mathf.Max(1, activeClip.audioClip.frequency / 12);
            if (activeClip.audioClip == lastPreviewedAudioClip &&
                Mathf.Abs(startSample - lastPreviewedAudioSample) < sampleThreshold)
            {
                return;
            }

            PlayPreviewAudio(activeClip.audioClip, startSample);
        }

        private static CombatClip GetActiveAudioClip(CombatSequenceAsset sequence, float time)
        {
            for (int trackIndex = sequence.Tracks.Count - 1; trackIndex >= 0; trackIndex--)
            {
                CombatTrack track = sequence.Tracks[trackIndex];
                if (track == null || track.trackType != CombatTrackType.Audio || track.muted)
                {
                    continue;
                }

                for (int clipIndex = track.clips.Count - 1; clipIndex >= 0; clipIndex--)
                {
                    CombatClip clip = track.clips[clipIndex];
                    if (clip != null && clip.audioClip != null && clip.startTime <= time && clip.EndTime >= time)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static void PlayPreviewAudio(AudioClip audioClip, int startSample)
        {
            EditorAudioUtil.StopAllPreviewClips();
            EditorAudioUtil.PlayPreviewClip(audioClip, startSample, false);
            lastPreviewedAudioClip = audioClip;
            lastPreviewedAudioSample = startSample;
        }
    }

    internal static class EditorAudioUtil
    {
        private static readonly Type AudioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo PlayPreviewClipMethod =
            AudioUtilType?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null) ??
            AudioUtilType?.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        private static readonly MethodInfo StopAllPreviewClipsMethod =
            AudioUtilType?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public) ??
            AudioUtilType?.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);

        public static bool IsAvailable => AudioUtilType != null && PlayPreviewClipMethod != null && StopAllPreviewClipsMethod != null;

        public static void PlayPreviewClip(AudioClip clip, int startSample, bool loop)
        {
            if (!IsAvailable || clip == null)
            {
                return;
            }

            PlayPreviewClipMethod.Invoke(null, new object[] { clip, startSample, loop });
        }

        public static void StopAllPreviewClips()
        {
            if (!IsAvailable)
            {
                return;
            }

            StopAllPreviewClipsMethod.Invoke(null, null);
        }
    }
}
