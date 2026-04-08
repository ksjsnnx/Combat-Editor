using System;
using UnityEditor;
using UnityEngine;

namespace NewCombatSystem.CombatEditor.Editor
{
    /// <summary>
    /// 战斗序列编辑器窗口
    /// </summary>
    public sealed class CombatSequenceEditorWindow : EditorWindow
    {
        // 界面布局常量
        private const float ToolbarHeight = 26f; // 顶部工具栏高度
        private const float RulerHeight = 24f; // 时间轴刻度高度
        private const float TrackHeaderWidth = 190f; // 左侧轨道头部宽度
        private const float TrackRowHeight = 44f; // 单个轨道行的高度
        private const float MinClipPixelWidth = 18f; //片段在 UI 上的最小像素宽度
        private const float InspectorWidth = 400f;// 右侧属性面板的宽度
        private const float ResizeHandleWidth = 6f; //调整片段边缘大小时的手柄宽度

        private CombatSequenceAsset sequence;// 当前正在编辑的战斗序列资源
        private CombatSequencePreviewBindings previewBindings;//场景中的预览对象绑定
        private Vector2 timelineScroll;// 时间轴区域的滚动位置
        private Vector2 inspectorScroll;
        private float zoom = 120f; // 时间轴的缩放倍率（单位像素/秒）
        private float currentTime;// 编辑器当前的播放时间点
        private bool isPlaying;// 编辑器是否处于播放预览状态
        private double lastEditorTime;// 上一次编辑器更新的时间戳
        private string selectedTrackGuid; // 当前选中轨道的GUID
        private string selectedClipGuid;// 当前选中的片段 GUID
        private DragMode dragMode;// 当前拖拽模式
        private string dragTrackGuid;// 当前拖拽的轨道GUID
        private string dragClipGuid;// 当前拖拽的片段GUID
        private Vector2 dragStartMouse;// 拖拽开始时鼠标位置
        private float dragStartTimeValue;// 拖拽开始时片段的起始时间
        private float dragStartDurationValue;// 拖拽开始时片段的持续时长

        private GUIStyle clipLabelStyle;
        private GUIStyle inspectorTitleStyle;

        // 拖拽模式枚举
        private enum DragMode
        {
            None,
            Scrub,           // 拖动时间轴刻度（洗带）
            MoveClip,        // 移动片段
            ResizeClipLeft,  // 向左调整片段大小
            ResizeClipRight  // 向右调整片段大小
        }

        [MenuItem("Window/Combat/Combat Sequence Editor")]
        public static void OpenWindow()
        {
            GetWindow<CombatSequenceEditorWindow>("Combat Editor");
        }

        /// <summary> 打开编辑器并关联指定的资源 </summary>
        public static void Open(CombatSequenceAsset asset)
        {
            CombatSequenceEditorWindow window = GetWindow<CombatSequenceEditorWindow>("Combat Editor");
            window.sequence = asset;
            window.currentTime = 0f;
            window.Focus();
        }

        private void OnEnable()
        {
            // 订阅编辑器更新，用于处理播放预览
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            // 窗口关闭时停止预览并恢复状态
            CombatSequenceEditorPreview.Stop(previewBindings);
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (sequence == null)
            {
                DrawEmptyState();
                return;
            }

            // 确保数据有效（排序、GUID分配等）
            sequence.EnsureValid();

            // 计算布局矩形
            Rect bodyRect = new Rect(0f, ToolbarHeight, position.width, position.height - ToolbarHeight);
            Rect timelineRect = new Rect(bodyRect.x, bodyRect.y, Mathf.Max(100f, bodyRect.width - InspectorWidth), bodyRect.height);
            Rect inspectorRect = new Rect(timelineRect.xMax, bodyRect.y, InspectorWidth, bodyRect.height);

            DrawTimelineArea(timelineRect);
            DrawInspector(inspectorRect);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(sequence);
            }

            // 如果有预览绑定，在编辑器下进行采样预览
            SamplePreviewIfNeeded();
        }

        /// <summary> 绘制顶部工具栏 </summary>
        private void DrawToolbar()
        {
            Rect toolbarRect = new Rect(0f, 0f, position.width, ToolbarHeight);
            GUILayout.BeginArea(toolbarRect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            sequence = (CombatSequenceAsset)EditorGUILayout.ObjectField(sequence, typeof(CombatSequenceAsset), false, GUILayout.Width(260f));
            if (EditorGUI.EndChangeCheck())
            {
                CombatSequenceEditorPreview.Stop(previewBindings);
                currentTime = 0f;
                selectedTrackGuid = null;
                selectedClipGuid = null;
                isPlaying = false;
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                CreateNewSequence();
            }

            if (sequence != null)
            {
                // 播放控制
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    TogglePlay();
                }

                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                {
                    isPlaying = false;
                    currentTime = 0f;
                }

                GUILayout.Space(6f);
                GUILayout.Label("Time", GUILayout.Width(32f));
                currentTime = EditorGUILayout.FloatField(currentTime, GUILayout.Width(52f));
                currentTime = Mathf.Clamp(currentTime, 0f, sequence.Duration);

                // 序列参数
                GUILayout.Space(6f);
                GUILayout.Label("Length", GUILayout.Width(40f));
                EditorGUI.BeginChangeCheck();
                float duration = EditorGUILayout.FloatField(sequence.Duration, GUILayout.Width(52f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sequence, "Change Sequence Duration");
                    sequence.Duration = duration;
                }

                GUILayout.Space(6f);
                GUILayout.Label("FPS", GUILayout.Width(24f));
                EditorGUI.BeginChangeCheck();
                float frameRate = EditorGUILayout.FloatField(sequence.FrameRate, GUILayout.Width(42f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sequence, "Change Sequence FPS");
                    sequence.FrameRate = frameRate;
                }

                GUILayout.FlexibleSpace();
                // 预览绑定对象选择
                previewBindings = (CombatSequencePreviewBindings)EditorGUILayout.ObjectField(
                    previewBindings,
                    typeof(CombatSequencePreviewBindings),
                    true,
                    GUILayout.Width(220f));
                GUILayout.Space(4f);
                GUILayout.Label("Zoom", GUILayout.Width(34f));
                zoom = GUILayout.HorizontalSlider(zoom, 50f, 240f, GUILayout.Width(100f));

                if (GUILayout.Button("Add Track", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    ShowAddTrackMenu();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary> 绘制未选择资源时的空白状态 </summary>
        private void DrawEmptyState()
        {
            Rect rect = new Rect(24f, 54f, position.width - 48f, position.height - 78f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(rect);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Select a CombatSequenceAsset or create a sample sequence from Assets/Create/Combat.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(8f);
            if (GUILayout.Button("Create Empty Sequence", GUILayout.Height(30f)))
            {
                CreateNewSequence();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        /// <summary> 绘制时间轴核心区域 </summary>
        private void DrawTimelineArea(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);

            float contentWidth = TrackHeaderWidth + sequence.Duration * zoom + 120f;
            float contentHeight = RulerHeight + Mathf.Max(1, sequence.Tracks.Count) * TrackRowHeight;
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
            timelineScroll = GUI.BeginScrollView(rect, timelineScroll, viewRect);

            // 绘制刻度尺
            DrawRuler(new Rect(0f, 0f, contentWidth, RulerHeight));

            Event evt = Event.current;
            // 绘制轨道
            for (int i = 0; i < sequence.Tracks.Count; i++)
            {
                CombatTrack track = sequence.Tracks[i];
                Rect rowRect = new Rect(0f, RulerHeight + i * TrackRowHeight, contentWidth, TrackRowHeight);
                DrawTrackRow(track, rowRect, evt);
            }

            // 绘制播放头（红色竖线）
            DrawPlayhead(contentHeight);
            // 处理交互输入
            HandleTimelineInput(rect, evt, viewRect);
            GUI.EndScrollView();
        }

        /// <summary> 绘制刻度尺 </summary>
        private void DrawRuler(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(TrackHeaderWidth, rect.y, 1f, rect.height), new Color(0.26f, 0.26f, 0.26f, 1f));

            int totalSteps = Mathf.CeilToInt(sequence.Duration * sequence.FrameRate);
            float secondsPerStep = 1f / sequence.FrameRate;
            for (int i = 0; i <= totalSteps; i++)
            {
                float time = i * secondsPerStep;
                float x = TrackHeaderWidth + time * zoom;
                bool isMajor = i % Mathf.RoundToInt(sequence.FrameRate) == 0;
                float lineHeight = isMajor ? rect.height : rect.height * 0.45f;
                Color lineColor = isMajor ? new Color(0.44f, 0.44f, 0.44f, 1f) : new Color(0.26f, 0.26f, 0.26f, 1f);
                EditorGUI.DrawRect(new Rect(x, rect.height - lineHeight, 1f, lineHeight), lineColor);

                if (isMajor)
                {
                    GUI.Label(new Rect(x + 4f, 2f, 40f, 16f), $"{time:0.0}s");
                }
            }
        }

        /// <summary> 绘制单行轨道 </summary>
        private void DrawTrackRow(CombatTrack track, Rect rowRect, Event evt)
        {
            Color rowColor = new Color(0.19f, 0.19f, 0.19f, 1f);
            if (selectedTrackGuid == track.guid)
            {
                rowColor = new Color(0.22f, 0.24f, 0.28f, 1f);
            }

            EditorGUI.DrawRect(rowRect, rowColor);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), new Color(0.12f, 0.12f, 0.12f, 1f));

            Rect headerRect = new Rect(rowRect.x, rowRect.y, TrackHeaderWidth, rowRect.height);
            DrawTrackHeader(track, headerRect, evt);

            Rect timelineRect = new Rect(headerRect.xMax, rowRect.y, rowRect.width - headerRect.width, rowRect.height);
            DrawTrackGrid(timelineRect);

            // 绘制该轨道下的所有片段
            foreach (CombatClip clip in track.clips)
            {
                DrawClip(track, clip, timelineRect, evt);
            }

            // 处理轨道背景点击
            if (evt.type == EventType.MouseDown && evt.button == 0 && timelineRect.Contains(evt.mousePosition))
            {
                selectedTrackGuid = track.guid;
                selectedClipGuid = null;
                Repaint();
            }
        }

        /// <summary> 绘制轨道头部（包含名称、类型、控制按钮） </summary>
        private void DrawTrackHeader(CombatTrack track, Rect rect, Event evt)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0.24f, 0.24f, 0.24f, 1f));

            Rect colorRect = new Rect(rect.x + 6f, rect.y + 8f, 8f, rect.height - 16f);
            EditorGUI.DrawRect(colorRect, track.color);

            GUI.Label(new Rect(rect.x + 22f, rect.y + 6f, 96f, 18f), track.displayName);
            GUI.Label(new Rect(rect.x + 22f, rect.y + 22f, 82f, 16f), track.trackType.ToString(), EditorStyles.miniLabel);

            Rect muteRect = new Rect(rect.x + 112f, rect.y + 10f, 26f, 20f);
            Rect lockRect = new Rect(rect.x + 140f, rect.y + 10f, 26f, 20f);
            Rect addRect = new Rect(rect.x + 168f, rect.y + 10f, 18f, 20f);

            EditorGUI.BeginChangeCheck();
            bool muted = GUI.Toggle(muteRect, track.muted, "M", EditorStyles.miniButton);
            bool locked = GUI.Toggle(lockRect, track.locked, "L", EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sequence, "Toggle Track State");
                track.muted = muted;
                track.locked = locked;
            }

            if (GUI.Button(addRect, "+", EditorStyles.miniButtonLeft))
            {
                Undo.RecordObject(sequence, "Add Combat Clip");
                CombatClip clip = sequence.AddClip(track, currentTime);
                selectedTrackGuid = track.guid;
                selectedClipGuid = clip.guid;
                GUI.changed = true;
            }

            // 头部右键菜单
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Clip"), false, () =>
                {
                    Undo.RecordObject(sequence, "Add Combat Clip");
                    CombatClip clip = sequence.AddClip(track, currentTime);
                    selectedTrackGuid = track.guid;
                    selectedClipGuid = clip.guid;
                    Repaint();
                });
                menu.AddItem(new GUIContent("Duplicate Track"), false, () => DuplicateTrack(track));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete Track"), false, () => DeleteTrack(track));
                menu.ShowAsContext();
                evt.Use();
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                selectedTrackGuid = track.guid;
                selectedClipGuid = null;
                Repaint();
            }
        }

        /// <summary> 绘制轨道网格线 </summary>
        private void DrawTrackGrid(Rect rect)
        {
            int majorTicks = Mathf.CeilToInt(sequence.Duration);
            for (int i = 0; i <= majorTicks; i++)
            {
                float x = rect.x + i * zoom;
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), new Color(0.24f, 0.24f, 0.24f, 1f));
            }
        }

        /// <summary> 绘制单个片段块 </summary>
        private void DrawClip(CombatTrack track, CombatClip clip, Rect timelineRect, Event evt)
        {
            float x = timelineRect.x + clip.startTime * zoom;
            float width = Mathf.Max(MinClipPixelWidth, clip.duration * zoom);
            Rect clipRect = new Rect(x, timelineRect.y + 6f, width, timelineRect.height - 12f);
            bool isSelected = selectedClipGuid == clip.guid;

            Color clipColor = clip.color.a > 0f ? clip.color : track.color;
            if (track.muted)
            {
                clipColor *= 0.45f;
            }

            EditorGUI.DrawRect(clipRect, clipColor);
            EditorGUI.DrawRect(new Rect(clipRect.x, clipRect.y, clipRect.width, 1f), Color.white * 0.12f);
            EditorGUI.DrawRect(new Rect(clipRect.x, clipRect.yMax - 1f, clipRect.width, 1f), Color.black * 0.35f);

            // 绘制选中边框
            if (isSelected)
            {
                Handles.BeginGUI();
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(
                    2f,
                    new Vector3(clipRect.x, clipRect.y),
                    new Vector3(clipRect.xMax, clipRect.y),
                    new Vector3(clipRect.xMax, clipRect.yMax),
                    new Vector3(clipRect.x, clipRect.yMax),
                    new Vector3(clipRect.x, clipRect.y));
                Handles.EndGUI();
            }

            GUI.Label(clipRect, clip.displayName, clipLabelStyle);

            // 处理左右边缘的大小调整手柄
            Rect leftHandle = new Rect(clipRect.x, clipRect.y, ResizeHandleWidth, clipRect.height);
            Rect rightHandle = new Rect(clipRect.xMax - ResizeHandleWidth, clipRect.y, ResizeHandleWidth, clipRect.height);
            EditorGUIUtility.AddCursorRect(leftHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);

            if (track.locked)
            {
                return;
            }

            // 片段拖拽交互
            if (evt.type == EventType.MouseDown && evt.button == 0 && clipRect.Contains(evt.mousePosition))
            {
                selectedTrackGuid = track.guid;
                selectedClipGuid = clip.guid;
                dragTrackGuid = track.guid;
                dragClipGuid = clip.guid;
                dragStartMouse = evt.mousePosition;
                dragStartTimeValue = clip.startTime;
                dragStartDurationValue = clip.duration;
                dragMode = rightHandle.Contains(evt.mousePosition)
                    ? DragMode.ResizeClipRight
                    : leftHandle.Contains(evt.mousePosition)
                        ? DragMode.ResizeClipLeft
                        : DragMode.MoveClip;
                Undo.RecordObject(sequence, "Edit Combat Clip");
                evt.Use();
            }

            // 片段右键菜单
            if (evt.type == EventType.ContextClick && clipRect.Contains(evt.mousePosition))
            {
                selectedTrackGuid = track.guid;
                selectedClipGuid = clip.guid;
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Duplicate Clip"), false, () => DuplicateClip(track, clip));
                menu.AddItem(new GUIContent("Delete Clip"), false, () => DeleteClip(track, clip));
                menu.ShowAsContext();
                evt.Use();
            }
        }

        /// <summary> 绘制播放头（当前时间线） </summary>
        private void DrawPlayhead(float contentHeight)
        {
            float x = TrackHeaderWidth + currentTime * zoom;
            EditorGUI.DrawRect(new Rect(x, 0f, 2f, contentHeight), new Color(1f, 0.28f, 0.28f, 1f));
            EditorGUI.DrawRect(new Rect(x - 5f, 0f, 12f, 12f), new Color(1f, 0.28f, 0.28f, 1f));
        }

        /// <summary> 处理时间轴区域的交互输入 </summary>
        private void HandleTimelineInput(Rect visibleRect, Event evt, Rect viewRect)
        {
            Rect rulerTimelineRect = new Rect(TrackHeaderWidth, 0f, viewRect.width - TrackHeaderWidth, RulerHeight);

            // 在刻度尺上按下鼠标开始洗带
            if (evt.type == EventType.MouseDown && evt.button == 0 && rulerTimelineRect.Contains(evt.mousePosition))
            {
                dragMode = DragMode.Scrub;
                UpdatePlayheadFromMouse(evt.mousePosition.x);
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag)
            {
                switch (dragMode)
                {
                    case DragMode.Scrub:
                        UpdatePlayheadFromMouse(evt.mousePosition.x);
                        evt.Use();
                        break;
                    case DragMode.MoveClip:
                    case DragMode.ResizeClipLeft:
                    case DragMode.ResizeClipRight:
                        HandleClipDrag(evt);
                        evt.Use();
                        break;
                }
            }

            if (evt.type == EventType.MouseUp)
            {
                dragMode = DragMode.None;
                dragTrackGuid = null;
                dragClipGuid = null;
            }

            // 处理滚轮缩放
            if (evt.type == EventType.ScrollWheel && visibleRect.Contains(evt.mousePosition))
            {
                float delta = -evt.delta.y * 4f;
                zoom = Mathf.Clamp(zoom + delta, 50f, 240f);
                evt.Use();
            }
        }

        /// <summary> 处理片段的移动和缩放拖拽 </summary>
        private void HandleClipDrag(Event evt)
        {
            CombatTrack track = sequence.GetTrack(dragTrackGuid);
            CombatClip clip = sequence.GetClip(dragTrackGuid, dragClipGuid);
            if (track == null || clip == null)
            {
                return;
            }

            float deltaSeconds = (evt.mousePosition.x - dragStartMouse.x) / zoom;
            switch (dragMode)
            {
                case DragMode.MoveClip:
                    clip.startTime = Mathf.Clamp(dragStartTimeValue + deltaSeconds, 0f, sequence.Duration);
                    break;
                case DragMode.ResizeClipLeft:
                    float endTime = dragStartTimeValue + dragStartDurationValue;
                    float newStart = Mathf.Clamp(dragStartTimeValue + deltaSeconds, 0f, endTime - 0.05f);
                    clip.duration = endTime - newStart;
                    clip.startTime = newStart;
                    break;
                case DragMode.ResizeClipRight:
                    clip.duration = Mathf.Clamp(dragStartDurationValue + deltaSeconds, 0.05f, sequence.Duration - clip.startTime);
                    break;
            }

            sequence.EnsureValid();
            Repaint();
        }

        /// <summary> 绘制右侧属性检查器(Inspector) </summary>
        private void DrawInspector(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);

            // 根据当前选择项显示不同的编辑界面
            if (!string.IsNullOrEmpty(selectedClipGuid) && !string.IsNullOrEmpty(selectedTrackGuid))
            {
                DrawClipInspector();
            }
            else if (!string.IsNullOrEmpty(selectedTrackGuid))
            {
                DrawTrackInspector();
            }
            else
            {
                DrawSequenceInspector();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary> 绘制序列全局属性编辑 </summary>
        private void DrawSequenceInspector()
        {
            GUILayout.Label("Sequence", inspectorTitleStyle);
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Name", sequence.name);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sequence, "Rename Sequence");
                sequence.name = newName;
            }

            EditorGUILayout.FloatField("Duration", sequence.Duration);
            EditorGUILayout.FloatField("Frame Rate", sequence.FrameRate);
            EditorGUILayout.IntField("Track Count", sequence.Tracks.Count);
            EditorGUILayout.HelpBox("Select a track or clip to edit its settings. Hitbox and Event tracks are intended for damage windows, combo timing, and gameplay hooks.", MessageType.Info);
        }

        /// <summary> 绘制轨道属性编辑 </summary>
        private void DrawTrackInspector()
        {
            CombatTrack track = sequence.GetTrack(selectedTrackGuid);
            if (track == null)
            {
                return;
            }

            GUILayout.Label("Track", inspectorTitleStyle);

            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Display Name", track.displayName);
            Color color = EditorGUILayout.ColorField("Color", track.color);
            bool muted = EditorGUILayout.Toggle("Muted", track.muted);
            bool locked = EditorGUILayout.Toggle("Locked", track.locked);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sequence, "Edit Track");
                track.displayName = name;
                track.color = color;
                track.muted = muted;
                track.locked = locked;
            }

            EditorGUILayout.EnumPopup("Type", track.trackType);
            EditorGUILayout.IntField("Clip Count", track.clips.Count);
            EditorGUILayout.HelpBox("A practical ARPG skill layout usually includes Animation, Movement, Hitbox, Effect, and Event tracks so design and code can iterate independently.", MessageType.None);
        }

        /// <summary> 绘制片段属性编辑 </summary>
        private void DrawClipInspector()
        {
            CombatTrack track = sequence.GetTrack(selectedTrackGuid);
            CombatClip clip = sequence.GetClip(selectedTrackGuid, selectedClipGuid);
            if (track == null || clip == null)
            {
                return;
            }

            GUILayout.Label("Clip", inspectorTitleStyle);

            EditorGUI.BeginChangeCheck();
            string clipName = EditorGUILayout.TextField("Display Name", clip.displayName);
            float startTime = EditorGUILayout.FloatField("Start Time", clip.startTime);
            float duration = EditorGUILayout.FloatField("Duration", clip.duration);
            Color color = EditorGUILayout.ColorField("Color", clip.color);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sequence, "Edit Clip Base");
                clip.displayName = clipName;
                clip.startTime = Mathf.Clamp(startTime, 0f, sequence.Duration);
                clip.duration = Mathf.Clamp(duration, 0.05f, sequence.Duration - clip.startTime);
                clip.color = color;
            }

            EditorGUILayout.Space(6f);
            // 绘制不同类型片段特有的负载数据
            DrawClipPayload(track.trackType, clip);
        }

        /// <summary> 绘制片段负载数据编辑（根据轨道类型切换字段） </summary>
        private void DrawClipPayload(CombatTrackType trackType, CombatClip clip)
        {
            EditorGUI.BeginChangeCheck();

            switch (trackType)
            {
                case CombatTrackType.Animation:
                    clip.animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", clip.animationClip, typeof(AnimationClip), false);
                    clip.animationState = EditorGUILayout.TextField("State Name对应Animator里的状态名称", clip.animationState);
                    clip.animationSpeed = EditorGUILayout.FloatField("Speed", clip.animationSpeed);
                    break;
                case CombatTrackType.Movement:
                    clip.moveOffset = EditorGUILayout.Vector3Field("Move Offset", clip.moveOffset);
                    clip.moveCurve = EditorGUILayout.CurveField("Move Curve", clip.moveCurve);
                    break;
                case CombatTrackType.Hitbox:
                    clip.hitboxOffset = EditorGUILayout.Vector3Field("Offset", clip.hitboxOffset);
                    clip.hitboxRadius = EditorGUILayout.FloatField("Radius", clip.hitboxRadius);
                    clip.damage = EditorGUILayout.IntField("Damage", clip.damage);
                    clip.poiseDamage = EditorGUILayout.IntField("Poise Damage", clip.poiseDamage);
                    clip.hitboxTag = EditorGUILayout.TextField("Tag", clip.hitboxTag);
                    break;
                case CombatTrackType.Effect:
                    clip.effectPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", clip.effectPrefab, typeof(GameObject), false);
                    clip.effectOffset = EditorGUILayout.Vector3Field("Offset", clip.effectOffset);
                    clip.effectScale = EditorGUILayout.Vector3Field("Scale", clip.effectScale);
                    break;
                case CombatTrackType.Audio:
                    clip.audioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", clip.audioClip, typeof(AudioClip), false);
                    clip.audioVolume = EditorGUILayout.Slider("Volume", clip.audioVolume, 0f, 1f);
                    break;
                case CombatTrackType.Camera:
                    clip.cameraShake = EditorGUILayout.Vector3Field("Shake", clip.cameraShake);
                    clip.cameraFovDelta = EditorGUILayout.FloatField("FOV Delta", clip.cameraFovDelta);
                    break;
                case CombatTrackType.Event:
                    clip.eventName = EditorGUILayout.TextField("Event Name", clip.eventName);
                    clip.stringPayload = EditorGUILayout.TextField("String", clip.stringPayload);
                    clip.intPayload = EditorGUILayout.IntField("Int", clip.intPayload);
                    clip.floatPayload = EditorGUILayout.FloatField("Float", clip.floatPayload);
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sequence, "Edit Clip Payload");
            }
        }

        private void OnEditorUpdate()
        {
            if (!isPlaying || sequence == null)
            {
                lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (lastEditorTime <= 0d)
            {
                lastEditorTime = now;
                return;
            }

            float deltaTime = (float)(now - lastEditorTime);
            currentTime += deltaTime;
            if (currentTime > sequence.Duration)
            {
                currentTime = 0f;
                isPlaying = false;
            }

            lastEditorTime = now;
            Repaint();
            SamplePreviewIfNeeded();
        }

        private void TogglePlay()
        {
            isPlaying = !isPlaying;
            lastEditorTime = EditorApplication.timeSinceStartup;
        }

        /// <summary> 根据鼠标点击位置更新当前播放头时间 </summary>
        private void UpdatePlayheadFromMouse(float mouseX)
        {
            currentTime = Mathf.Clamp((mouseX - TrackHeaderWidth) / zoom, 0f, sequence.Duration);
            isPlaying = false;
            Repaint();
        }

        /// <summary> 显示添加轨道的快捷菜单 </summary>
        private void ShowAddTrackMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (CombatTrackType trackType in Enum.GetValues(typeof(CombatTrackType)))
            {
                CombatTrackType capturedType = trackType;
                menu.AddItem(new GUIContent(trackType.ToString()), false, () =>
                {
                    Undo.RecordObject(sequence, "Add Combat Track");
                    CombatTrack track = sequence.AddTrack(capturedType);
                    selectedTrackGuid = track.guid;
                    selectedClipGuid = null;
                    Repaint();
                });
            }

            menu.ShowAsContext();
        }

        /// <summary> 复制整个轨道 </summary>
        private void DuplicateTrack(CombatTrack source)
        {
            Undo.RecordObject(sequence, "Duplicate Combat Track");
            CombatTrack track = new CombatTrack
            {
                displayName = source.displayName,
                trackType = source.trackType,
                color = source.color,
                muted = source.muted,
                locked = source.locked
            };

            foreach (CombatClip sourceClip in source.clips)
            {
                track.clips.Add(CopyClip(sourceClip, sourceClip.startTime));
            }

            sequence.Tracks.Add(track);
            sequence.EnsureValid();
            selectedTrackGuid = track.guid;
            selectedClipGuid = null;
        }

        /// <summary> 删除轨道 </summary>
        private void DeleteTrack(CombatTrack track)
        {
            Undo.RecordObject(sequence, "Delete Combat Track");
            sequence.Tracks.Remove(track);
            selectedTrackGuid = null;
            selectedClipGuid = null;
        }

        /// <summary> 复制片段 </summary>
        private void DuplicateClip(CombatTrack track, CombatClip source)
        {
            Undo.RecordObject(sequence, "Duplicate Combat Clip");
            CombatClip clip = CopyClip(source, Mathf.Min(sequence.Duration, source.startTime + 0.05f));
            track.clips.Add(clip);
            sequence.EnsureValid();
            selectedTrackGuid = track.guid;
            selectedClipGuid = clip.guid;
        }

        /// <summary> 删除片段 </summary>
        private void DeleteClip(CombatTrack track, CombatClip clip)
        {
            Undo.RecordObject(sequence, "Delete Combat Clip");
            track.clips.Remove(clip);
            selectedClipGuid = null;
        }

        /// <summary> 深拷贝一个片段对象 </summary>
        private CombatClip CopyClip(CombatClip source, float startTime)
        {
            return new CombatClip
            {
                displayName = source.displayName + " Copy",
                startTime = startTime,
                duration = source.duration,
                color = source.color,
                animationClip = source.animationClip,
                animationState = source.animationState,
                animationSpeed = source.animationSpeed,
                moveOffset = source.moveOffset,
                moveCurve = source.moveCurve,
                hitboxOffset = source.hitboxOffset,
                hitboxRadius = source.hitboxRadius,
                damage = source.damage,
                poiseDamage = source.poiseDamage,
                hitboxTag = source.hitboxTag,
                effectPrefab = source.effectPrefab,
                effectOffset = source.effectOffset,
                effectScale = source.effectScale,
                audioClip = source.audioClip,
                audioVolume = source.audioVolume,
                cameraShake = source.cameraShake,
                cameraFovDelta = source.cameraFovDelta,
                eventName = source.eventName,
                stringPayload = source.stringPayload,
                intPayload = source.intPayload,
                floatPayload = source.floatPayload
            };
        }

        /// <summary> 创建一个新的空战斗序列资源 </summary>
        private void CreateNewSequence()
        {
            CombatSequenceAsset asset = CreateInstance<CombatSequenceAsset>();
            asset.name = "CombatSequence";
            asset.Duration = 2f;
            asset.FrameRate = 30f;
            asset.AddTrack(CombatTrackType.Animation);
            asset.AddTrack(CombatTrackType.Hitbox);
            asset.AddTrack(CombatTrackType.Event);

            string path = EditorUtility.SaveFilePanelInProject("Create Combat Sequence", "CombatSequence", "asset", "Choose a location");
            if (string.IsNullOrEmpty(path))
            {
                DestroyImmediate(asset);
                return;
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            sequence = asset;
            selectedTrackGuid = null;
            selectedClipGuid = null;
            currentTime = 0f;
        }

        private void EnsureStyles()
        {
            if (clipLabelStyle == null)
            {
                clipLabelStyle = new GUIStyle(EditorStyles.whiteMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
            }

            if (inspectorTitleStyle == null)
            {
                inspectorTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13
                };
            }
        }

        private void SamplePreviewIfNeeded()
        {
            if (sequence == null)
            {
                CombatSequenceEditorPreview.Stop(previewBindings);
                return;
            }

            if (previewBindings != null)
            {
                CombatSequenceEditorPreview.Sample(sequence, previewBindings, currentTime);
            }
        }
    }

    /// <summary>
    /// 静态辅助类，负责编辑器下的实时预览（通过AnimationMode实现）
    /// </summary>
    public static class CombatSequenceEditorPreview
    {
        /// <summary> 对指定序列和时间点进行预览采样 </summary>
        public static void Sample(CombatSequenceAsset sequence, CombatSequencePreviewBindings bindings, float time)
        {
            if (sequence == null || bindings == null)
            {
                return;
            }

            // 记录当前状态，以便后续恢复
            bindings.CapturePreviewBasePose();
            ApplyMovement(sequence, bindings, time);
            ApplyAnimation(sequence, bindings, time);
            bindings.SetPreviewState(sequence, time);
            SceneView.RepaintAll();
        }

        /// <summary> 停止预览并清理状态 </summary>
        public static void Stop(CombatSequencePreviewBindings bindings)
        {
            if (bindings == null)
            {
                return;
            }

            bindings.RestorePreviewBasePose();
            bindings.SetPreviewState(null, 0f);

            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }

            SceneView.RepaintAll();
        }

        /// <summary> 在编辑器下应用位移预览 </summary>
        private static void ApplyMovement(CombatSequenceAsset sequence, CombatSequencePreviewBindings bindings, float time)
        {
            Vector3 worldOffset = Vector3.zero;
            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Movement || track.muted)
                {
                    continue;
                }

                foreach (CombatClip clip in track.clips)
                {
                    if (clip == null || time < clip.startTime || time > clip.EndTime)
                    {
                        continue;
                    }

                    float progress = Mathf.InverseLerp(clip.startTime, clip.EndTime, time);
                    float weight = clip.moveCurve == null ? progress : clip.moveCurve.Evaluate(progress);
                    worldOffset += bindings.ResolveWorldDirection(clip.moveOffset) * weight;
                }
            }

            bindings.MovementRoot.position = bindings.GetPreviewBasePosition() + worldOffset;
        }

        /// <summary> 在编辑器下应用动画采样预览（利用Unity的AnimationMode） </summary>
        private static void ApplyAnimation(CombatSequenceAsset sequence, CombatSequencePreviewBindings bindings, float time)
        {
            CombatClip activeAnimation = GetActiveAnimationClip(sequence, time);
            if (activeAnimation == null || activeAnimation.animationClip == null || bindings.AnimationRoot == null)
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }

                return;
            }

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            float localTime = Mathf.Clamp(time - activeAnimation.startTime, 0f, activeAnimation.duration);
            localTime *= Mathf.Max(0.01f, activeAnimation.animationSpeed);

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(bindings.AnimationRoot.gameObject, activeAnimation.animationClip, localTime);
            AnimationMode.EndSampling();
        }

        /// <summary> 查找当前时间点最适合预览的动画片段 </summary>
        private static CombatClip GetActiveAnimationClip(CombatSequenceAsset sequence, float time)
        {
            foreach (CombatTrack track in sequence.Tracks)
            {
                if (track == null || track.trackType != CombatTrackType.Animation || track.muted)
                {
                    continue;
                }

                for (int i = track.clips.Count - 1; i >= 0; i--)
                {
                    CombatClip clip = track.clips[i];
                    if (clip != null && clip.startTime <= time && clip.EndTime >= time)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }
    }
}
