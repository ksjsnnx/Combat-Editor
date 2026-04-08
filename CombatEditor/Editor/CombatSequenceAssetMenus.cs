using UnityEditor;
using UnityEngine;

namespace NewCombatSystem.CombatEditor.Editor
{
    /// <summary>
    /// 战斗序列资源菜单扩展，提供创建示例序列的功能
    /// </summary>
    public static class CombatSequenceAssetMenus
    {
        /// <summary>
        /// 在Assets菜单中添加一个选项，用于快速生成一个ARPG风格的战斗序列示例
        /// </summary>
        [MenuItem("Assets/Create/Combat/Create Sample ARPG Sequence", priority = 11)]
        public static void CreateSampleSequence()
        {
            // 创建并初始化资源
            CombatSequenceAsset asset = ScriptableObject.CreateInstance<CombatSequenceAsset>();
            asset.name = "Sample_ARPG_Slash_Combo";
            asset.Duration = 2.2f;
            asset.FrameRate = 30f;

            // 添加动画轨道和片段
            CombatTrack animationTrack = asset.AddTrack(CombatTrackType.Animation);
            CombatClip animClip = asset.AddClip(animationTrack, 0f);
            animClip.displayName = "Slash_A";
            animClip.duration = 0.85f;
            animClip.animationState = "Slash_A";

            // 添加位移轨道和片段
            CombatTrack movementTrack = asset.AddTrack(CombatTrackType.Movement);
            CombatClip moveClip = asset.AddClip(movementTrack, 0.08f);
            moveClip.displayName = "Step In";
            moveClip.duration = 0.28f;
            moveClip.moveOffset = new Vector3(0f, 0f, 1.3f);

            // 添加攻击判定轨道和片段
            CombatTrack hitboxTrack = asset.AddTrack(CombatTrackType.Hitbox);
            CombatClip hitClip = asset.AddClip(hitboxTrack, 0.24f);
            hitClip.displayName = "Main Hit";
            hitClip.duration = 0.14f;
            hitClip.damage = 45;
            hitClip.poiseDamage = 16;
            hitClip.hitboxRadius = 1f;

            // 添加特效轨道和片段
            CombatTrack effectTrack = asset.AddTrack(CombatTrackType.Effect);
            CombatClip effectClip = asset.AddClip(effectTrack, 0.2f);
            effectClip.displayName = "Sword Trail";
            effectClip.duration = 0.32f;

            // 添加音效轨道和片段
            CombatTrack audioTrack = asset.AddTrack(CombatTrackType.Audio);
            CombatClip audioClip = asset.AddClip(audioTrack, 0.18f);
            audioClip.displayName = "Slash SFX";
            audioClip.duration = 0.3f;

            // 添加游戏逻辑事件轨道和片段
            CombatTrack eventTrack = asset.AddTrack(CombatTrackType.Event);
            CombatClip eventClip = asset.AddClip(eventTrack, 0.75f);
            eventClip.displayName = "Combo Window Open";
            eventClip.duration = 0.12f;
            eventClip.eventName = "OpenComboWindow";
            eventClip.floatPayload = 0.35f;

            asset.EnsureValid();

            // 生成唯一路径并保存资源
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scripts/CombatEditor/Sample_ARPG_Slash_Combo.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;

            // 自动打开编辑器窗口进行编辑
            CombatSequenceEditorWindow.Open(asset);
        }
    }
}
