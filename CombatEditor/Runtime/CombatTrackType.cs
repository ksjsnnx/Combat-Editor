using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCombatSystem.CombatEditor
{
    /// <summary>
    /// 战斗轨道类型枚举，定义了战斗序列中不同功能的轨道
    /// </summary>
    public enum CombatTrackType
    {
        // 动画轨道
        Animation,
        // 位移轨道
        Movement,
        // 判定框/攻击包轨道
        Hitbox,
        // 特效轨道
        Effect,
        // 音效轨道
        Audio,
        // 摄像机效果轨道（如震屏、FOV变化）
        Camera,
        // 游戏事件轨道（触发逻辑回调）
        Event
    }

    /// <summary>
    /// 战斗片段类，代表轨道上的一个具体动作或事件
    /// </summary>
    [Serializable]
    public sealed class CombatClip
    {
        // 唯一标识符
        public string guid = Guid.NewGuid().ToString("N");
        // 显示名称
        public string displayName = "New Clip";
        // 开始时间（秒）
        public float startTime;
        // 持续时间（秒）
        public float duration = 0.25f;
        // 片段颜色（用于编辑器显示）
        public Color color = new Color(0.22f, 0.55f, 0.95f, 1f);

        [Header("Animation")]
        // 播放的动画剪辑
        public AnimationClip animationClip;
        // 动画状态名称
        public string animationState;
        // 播放速度
        public float animationSpeed = 1f;

        [Header("Movement")]
        // 位移偏移量
        public Vector3 moveOffset = new Vector3(0f, 0f, 1.5f);
        // 位移曲线
        public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Hitbox")]
        // 判定框偏移量
        public Vector3 hitboxOffset = new Vector3(0f, 1f, 1f);
        // 判定框半径
        public float hitboxRadius = 0.8f;
        // 伤害值
        public int damage = 30;
        // 削韧值/失衡伤害
        public int poiseDamage = 10;
        // 判定框标签
        public string hitboxTag = "Attack";

        [Header("Effect")]
        // 特效预制体
        public GameObject effectPrefab;
        // 特效偏移量
        public Vector3 effectOffset = new Vector3(0f, 1f, 1f);
        // 特效缩放
        public Vector3 effectScale = Vector3.one;

        [Header("Audio")]
        // 音频剪辑
        public AudioClip audioClip;
        // 音量
        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Header("Camera")]
        // 摄像机震动强度
        public Vector3 cameraShake = new Vector3(0.15f, 0.15f, 0.15f);
        // 视野(FOV)变化值
        public float cameraFovDelta = 3f;

        [Header("Gameplay Event")]
        // 事件名称
        public string eventName = "OnSkillEvent";
        // 字符串负载数据
        public string stringPayload;
        // 整型负载数据
        public int intPayload;
        // 浮点型负载数据
        public float floatPayload;

        // 获取片段结束时间
        public float EndTime => startTime + Mathf.Max(0.01f, duration);
    }

    /// <summary>
    /// 战斗轨道类，包含多个战斗片段
    /// </summary>
    [Serializable]
    public sealed class CombatTrack
    {
        // 唯一标识符
        public string guid = Guid.NewGuid().ToString("N");
        // 轨道显示名称
        public string displayName = "New Track";
        // 轨道类型
        public CombatTrackType trackType;
        // 轨道颜色
        public Color color = new Color(0.22f, 0.55f, 0.95f, 1f);
        // 是否静音（禁用轨道）
        public bool muted;
        // 是否锁定（禁止编辑）
        public bool locked;
        // 轨道包含的片段列表
        public List<CombatClip> clips = new List<CombatClip>();
    }
}
