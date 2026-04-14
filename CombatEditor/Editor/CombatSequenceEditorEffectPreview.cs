using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NewCombatSystem.CombatEditor.Editor
{
    internal static class CombatSequenceEditorEffectPreview
    {
        private sealed class PreviewInstance
        {
            public CombatClip Clip;
            public GameObject Instance;
            public ParticleSystem[] ParticleSystems;
        }

        private static readonly Dictionary<string, PreviewInstance> ActiveInstances = new Dictionary<string, PreviewInstance>();

        private static CombatSequenceAsset lastSequence;
        private static CombatSequencePreviewBindings lastBindings;
        private static float lastSampleTime;
        private static bool hasLastSample;

        public static void Sample(CombatSequenceAsset sequence, CombatSequencePreviewBindings bindings, float time)
        {
            if (sequence == null || bindings == null)
            {
                Stop();
                return;
            }

            if (lastSequence != sequence || lastBindings != bindings)
            {
                Stop();
            }

            HashSet<string> activeClipGuids = new HashSet<string>();

            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Effect || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null || clip.effectPrefab == null)
                    {
                        continue;
                    }

                    if (time < clip.startTime || time > clip.EndTime)
                    {
                        continue;
                    }

                    activeClipGuids.Add(clip.guid);
                    PreviewInstance preview = GetOrCreatePreviewInstance(bindings, clip);
                    if (preview == null)
                    {
                        continue;
                    }

                    UpdatePreviewTransform(bindings, clip, preview.Instance);
                    SimulatePreview(preview, Mathf.Max(0f, time - clip.startTime), ShouldRestartSimulation(time));
                }
            }

            RemoveInactiveInstances(activeClipGuids);

            lastSequence = sequence;
            lastBindings = bindings;
            lastSampleTime = time;
            hasLastSample = true;
        }

        public static void Stop()
        {
            foreach (PreviewInstance preview in ActiveInstances.Values)
            {
                DestroyPreviewInstance(preview.Instance);
            }

            ActiveInstances.Clear();
            lastSequence = null;
            lastBindings = null;
            lastSampleTime = 0f;
            hasLastSample = false;
        }

        private static PreviewInstance GetOrCreatePreviewInstance(CombatSequencePreviewBindings bindings, CombatClip clip)
        {
            if (ActiveInstances.TryGetValue(clip.guid, out PreviewInstance existing))
            {
                return existing;
            }

            Transform effectRoot = bindings.EffectRoot;
            GameObject instance = PrefabUtility.InstantiatePrefab(clip.effectPrefab, effectRoot) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(clip.effectPrefab, effectRoot);
            }

            if (instance == null)
            {
                return null;
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            ApplyHideFlagsRecursively(instance.transform, HideFlags.HideAndDontSave);

            PreviewInstance preview = new PreviewInstance
            {
                Clip = clip,
                Instance = instance,
                ParticleSystems = instance.GetComponentsInChildren<ParticleSystem>(true)
            };

            ActiveInstances.Add(clip.guid, preview);
            return preview;
        }

        private static void UpdatePreviewTransform(CombatSequencePreviewBindings bindings, CombatClip clip, GameObject instance)
        {
            Transform ownerRoot = bindings.OwnerRoot;
            instance.transform.SetParent(bindings.EffectRoot, true);
            instance.transform.position = ownerRoot.TransformPoint(clip.effectOffset);
            instance.transform.rotation = ownerRoot.rotation * Quaternion.Euler(clip.effectRotation);
            instance.transform.localScale = clip.effectScale;
        }

        private static void SimulatePreview(PreviewInstance preview, float localTime, bool restart)
        {
            if (preview.Instance == null)
            {
                return;
            }

            if (preview.ParticleSystems == null || preview.ParticleSystems.Length == 0)
            {
                preview.Instance.SetActive(true);
                return;
            }

            for (int i = 0; i < preview.ParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = preview.ParticleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                if (restart)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                particleSystem.Simulate(localTime, true, true, true);
                particleSystem.Play(true);
            }
        }

        private static bool ShouldRestartSimulation(float time)
        {
            if (!hasLastSample)
            {
                return true;
            }

            if (time < lastSampleTime)
            {
                return true;
            }

            return time - lastSampleTime > 0.12f;
        }

        private static void RemoveInactiveInstances(HashSet<string> activeClipGuids)
        {
            List<string> guidsToRemove = null;
            foreach (KeyValuePair<string, PreviewInstance> pair in ActiveInstances)
            {
                if (activeClipGuids.Contains(pair.Key))
                {
                    continue;
                }

                guidsToRemove ??= new List<string>();
                guidsToRemove.Add(pair.Key);
                DestroyPreviewInstance(pair.Value.Instance);
            }

            if (guidsToRemove == null)
            {
                return;
            }

            for (int i = 0; i < guidsToRemove.Count; i++)
            {
                ActiveInstances.Remove(guidsToRemove[i]);
            }
        }

        private static void DestroyPreviewInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Object.DestroyImmediate(instance);
        }

        private static void ApplyHideFlagsRecursively(Transform root, HideFlags hideFlags)
        {
            root.hideFlags = hideFlags;
            for (int i = 0; i < root.childCount; i++)
            {
                ApplyHideFlagsRecursively(root.GetChild(i), hideFlags);
            }
        }
    }
}
