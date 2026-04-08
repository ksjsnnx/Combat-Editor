using UnityEditor;
using UnityEngine;

namespace NewCombatSystem.CombatEditor.Editor
{
    /// <summary>
    /// 战斗序列资源的自定义检查器(Inspector)，提供快捷打开编辑器窗口的按钮
    /// </summary>
    [CustomEditor(typeof(CombatSequenceAsset))]
    public sealed class CombatSequenceAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 绘制默认的Inspector界面
            DrawDefaultInspector();

            GUILayout.Space(8f);
            // 添加一个醒目的按钮用于打开专门的战斗编辑器窗口
            if (GUILayout.Button("Open Combat Editor", GUILayout.Height(30f)))
            {
                CombatSequenceEditorWindow.Open((CombatSequenceAsset)target);
            }
        }
    }
}
