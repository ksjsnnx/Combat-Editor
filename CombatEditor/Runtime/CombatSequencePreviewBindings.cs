using UnityEngine;

namespace NewCombatSystem.CombatEditor
{
    /// <summary>
    /// 战斗序列预览绑定组件，负责关联场景中的物体并处理编辑器下的预览逻辑（如Gizmos绘制）
    /// </summary>
    public sealed class CombatSequencePreviewBindings : MonoBehaviour
    {
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private Transform animationRoot;
        [SerializeField] private Transform movementRoot;
        [SerializeField] private Transform effectRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool drawHitboxGizmos = true;
        [SerializeField] private bool drawMovementGizmos = true;

        [SerializeField, HideInInspector] private CombatSequenceAsset previewSequence;
        [SerializeField, HideInInspector] private float previewTime;
        [SerializeField, HideInInspector] private Vector3 previewBasePosition;
        [SerializeField, HideInInspector] private bool hasPreviewBasePosition;

        // 拥有者根节点，用于坐标转换
        public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform;
        // 动画根节点
        public Transform AnimationRoot => animationRoot != null ? animationRoot : OwnerRoot;
        // 位移根节点
        public Transform MovementRoot => movementRoot != null ? movementRoot : OwnerRoot;
        // 特效根节点
        public Transform EffectRoot => effectRoot != null ? effectRoot : OwnerRoot;
        // 动画控制器引用
        public Animator Animator => animator;
        // 音源引用
        public AudioSource AudioSource => audioSource;

        /// <summary> 捕获当前位移根节点的位置作为预览基准点 </summary>
        public void CapturePreviewBasePose()
        {
            previewBasePosition = MovementRoot.position;
            hasPreviewBasePosition = true;
        }

        /// <summary> 恢复位移根节点到预览前的基准点 </summary>
        public void RestorePreviewBasePose()
        {
            if (!hasPreviewBasePosition)
            {
                return;
            }

            MovementRoot.position = previewBasePosition;
        }

        /// <summary> 获取预览基准位置 </summary>
        public Vector3 GetPreviewBasePosition()
        {
            if (!hasPreviewBasePosition)
            {
                CapturePreviewBasePose();
            }

            return previewBasePosition;
        }

        /// <summary> 将局部坐标偏移转换为世界坐标 </summary>
        public Vector3 ResolveWorldPoint(Vector3 localOffset)
        {
            return OwnerRoot.TransformPoint(localOffset);
        }

        /// <summary> 将局部方向转换为世界方向 </summary>
        public Vector3 ResolveWorldDirection(Vector3 localDirection)
        {
            return OwnerRoot.TransformDirection(localDirection);
        }

        /// <summary> 设置当前的预览状态，供Gizmos绘制使用 </summary>
        public void SetPreviewState(CombatSequenceAsset sequence, float time)
        {
            previewSequence = sequence;
            previewTime = time;
        }

        private void OnDrawGizmosSelected()
        {
            if (previewSequence == null)
            {
                return;
            }

            if (drawMovementGizmos)
            {
                DrawMovementGizmos();
            }

            if (drawHitboxGizmos)
            {
                DrawHitboxGizmos();
            }
        }

        /// <summary> 绘制位移轨迹预览 </summary>
        private void DrawMovementGizmos()
        {
            foreach (CombatTrack track in previewSequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Movement || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    Vector3 from = GetPreviewBasePosition();
                    Vector3 to = from + ResolveWorldDirection(clip.moveOffset);
                    Color color = Color.Lerp(track.color, Color.white, 0.15f);
                    Gizmos.color = new Color(color.r, color.g, color.b, 0.6f);
                    Gizmos.DrawLine(from, to);

                    // 如果当前片段处于活动状态，绘制一个球体标识
                    if (IsClipActive(clip, previewTime))
                    {
                        Gizmos.DrawSphere(MovementRoot.position, 0.08f);
                    }
                }
            }
        }

        /// <summary> 绘制判定框(Hitbox)预览 </summary>
        private void DrawHitboxGizmos()
        {
            foreach (CombatTrack track in previewSequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Hitbox || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null || !IsClipActive(clip, previewTime))
                    {
                        continue;
                    }

                    Vector3 center = ResolveWorldPoint(clip.hitboxOffset);
                    Color color = Color.Lerp(track.color, Color.white, 0.1f);
                    Gizmos.color = new Color(color.r, color.g, color.b, 0.45f);
                    Gizmos.DrawSphere(center, clip.hitboxRadius);
                    Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
                    Gizmos.DrawWireSphere(center, clip.hitboxRadius);
                }
            }
        }

        /// <summary> 检查片段是否在指定时间点处于活动状态 </summary>
        private static bool IsClipActive(CombatClip clip, float time)
        {
            return clip.startTime <= time && clip.EndTime >= time;
        }
    }
}
