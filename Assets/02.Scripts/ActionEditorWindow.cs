using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ActionEditorWindow : EditorWindow
{
    [SerializeField] ActionDefinition _def;
    SerializedObject _so;

    [SerializeField] int _currentFrame;
    [SerializeField] float _pixelsPerFrame = 16f;
    [SerializeField] float _scrollX;

    [SerializeField] GameObject _previewTarget;
    [SerializeField] bool _previewOn;
    [SerializeField] bool _playing;
    [SerializeField] float _playFps = 60f;
    enum SampleMode { Normalized, ClipFps }
    [SerializeField] SampleMode _sampleMode = SampleMode.Normalized;
    double _lastTick;
    double _playAccum;
    int _lastSampledFrame = -1;

    int _selLane = -1, _selIndex = -1;
    enum DragMode { None, Scrub, LeftEdge, RightEdge, Body, Point }
    DragMode _drag = DragMode.None;
    int _grabOffset;
    int _timelineCtrl;

    // layout constants
    const float TimelineTopPad = 8f;
    const float RulerH = 20f;
    const float RowH = 18f;
    const float RowGap = 2f;
    const float LanePad = 3f;
    const float LaneLabelW = 96f;
    const float Handle = 10f;
    const float MinPxF = 4f;
    const float MaxPxF = 48f;
    const int FrameStepBig = 5;

    int _scrubControl;

    enum LaneKind { Range, Point }
    struct Lane
    {
        public string Name;
        public Color Color;
        public LaneKind Kind;
        public string Prop;
    }

    static readonly Lane[] Lanes =
    {
        new Lane { Name = "Hit",    Color = new Color(0.90f, 0.30f, 0.25f), Kind = LaneKind.Range, Prop = "HitWindows" },
        new Lane { Name = "Cancel", Color = new Color(0.30f, 0.75f, 0.45f), Kind = LaneKind.Range, Prop = "CancelWindows" },
        new Lane { Name = "Invuln", Color = new Color(0.35f, 0.60f, 0.95f), Kind = LaneKind.Range, Prop = "InvulnWindows" },
        new Lane { Name = "Motion", Color = new Color(0.70f, 0.45f, 0.90f), Kind = LaneKind.Range, Prop = "MotionImpulses" },
        new Lane { Name = "Ranged", Color = new Color(0.95f, 0.80f, 0.25f), Kind = LaneKind.Point, Prop = "RangedFires" },
    };

    struct Bar { public int Index, Start, End, Row; public string Label; }; 

    readonly List<Bar>[] _bars = new List<Bar>[Lanes.Length];
    readonly int[] _rows = new int[Lanes.Length];
    readonly float[] _laneY = new float[Lanes.Length];
    readonly float[] _laneH = new float[Lanes.Length];
    float _lanesTotalH;

    [MenuItem("Moko/Action Editor")]
    static void Open() => GetWindow<ActionEditorWindow>("Action Editor");

    [UnityEditor.Callbacks.OnOpenAsset]
    static bool OnOpenAsset(int instanceID, int line)
    {
        var def = EditorUtility.InstanceIDToObject(instanceID) as ActionDefinition;
        if (def == null) return false;
        GetWindow<ActionEditorWindow>("Action Editor").SetTarget(def);
        return true;
    }

    void OnEnable() { EditorApplication.update += OnEditorUpdate; }
    void OnDisable() { EditorApplication.update -= OnEditorUpdate; ExitPreview(); }

    void SetTarget(ActionDefinition def)
    {
        _def = def; _so = def != null ? new SerializedObject(def) : null;
        _currentFrame = 0; _scrollX = 0; _selLane = _selIndex = -1; _drag = DragMode.None;
        _lastSampledFrame = -1;
        Repaint();
    }

    void EnsureSO()
    {
        if (_def == null) { _so = null; return; }
        if (_so == null || _so.targetObject != _def) _so = new SerializedObject(_def);
    }

    private void OnGUI()
    {

        DrawToolbar();

        if (_def == null)
        {
            EditorGUILayout.HelpBox("Select an ActionDefinition.\n (Double-Clike the asset, or assign it above.)", MessageType.Info);
            return;
        }

        EnsureSO();
        DrawPreviewBar();
        BuildLayout();

        GUILayout.Space(TimelineTopPad);

        float timelineH = RulerH + _lanesTotalH + 8f;
        Rect area = GUILayoutUtility.GetRect(0, 100000, timelineH, timelineH);

        HandleKeyboard();
        DrawTimeline(area);
        DrawInspector();

        SamplePoseIfNeeded();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            var def = (ActionDefinition)EditorGUILayout.ObjectField(_def, typeof(ActionDefinition), false, GUILayout.Width(220));
            if (EditorGUI.EndChangeCheck()) SetTarget(def);

            GUILayout.Space(12);

            using (new EditorGUI.DisabledScope(_def == null))
            {
                int total = _def != null ? Mathf.Max(1, _def.TotalFrames) : 1;
                GUILayout.Label("Frame", GUILayout.Width(40));
                EditorGUI.BeginChangeCheck();
                int f = EditorGUILayout.IntField(_currentFrame, GUILayout.Width(46));
                if (EditorGUI.EndChangeCheck()) _currentFrame = Mathf.Clamp(f, 0, total);
                GUILayout.Label("/ " + total, GUILayout.Width(40));

                if (GUILayout.Button("\u25C0", EditorStyles.toolbarButton, GUILayout.Width(24))) StepFrame(-1);
                if (GUILayout.Button("\u25B6", EditorStyles.toolbarButton, GUILayout.Width(24))) StepFrame(+1);
                if (GUILayout.Button("|\u25C0", EditorStyles.toolbarButton, GUILayout.Width(28))) { _currentFrame = 0; Repaint(); }
                if (GUILayout.Button("\u25B6|", EditorStyles.toolbarButton, GUILayout.Width(28))) { _currentFrame = total; Repaint(); }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Zoom", GUILayout.Width(38));
            _pixelsPerFrame = GUILayout.HorizontalSlider(_pixelsPerFrame, MinPxF, MaxPxF, GUILayout.Width(120));
        }
    }

    void DrawPreviewBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            bool on = GUILayout.Toggle(_previewOn, "Preview", EditorStyles.toolbarButton, GUILayout.Width(60));
            if (on != _previewOn) { _previewOn = on; if (on) EnterPreview(); else ExitPreview(); }

            EditorGUI.BeginChangeCheck();
            _previewTarget = (GameObject)EditorGUILayout.ObjectField(_previewTarget, typeof(GameObject), true, GUILayout.Width(150));
            if (EditorGUI.EndChangeCheck()) { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); _lastSampledFrame = -1; }

            if (GUILayout.Button("Use Selected", EditorStyles.toolbarButton, GUILayout.Width(86)) && Selection.activeGameObject != null)
            { _previewTarget = Selection.activeGameObject; _lastSampledFrame = -1; }

            using (new EditorGUI.DisabledScope(!_previewOn))
            {
                if (GUILayout.Button(_playing ? "\u275A\u275A Pause" : "\u25B6 Play", EditorStyles.toolbarButton, GUILayout.Width(70)))
                { _playing = !_playing; _lastTick = EditorApplication.timeSinceStartup; _playAccum = 0; }
                GUILayout.Label("fps", GUILayout.Width(24));
                _playFps = Mathf.Clamp(EditorGUILayout.FloatField(_playFps, GUILayout.Width(38)), 1f, 240f);
            }

            GUILayout.Space(10);
            _sampleMode = (SampleMode)EditorGUILayout.EnumPopup(_sampleMode, EditorStyles.toolbarPopup, GUILayout.Width(90));
            if (GUI.changed) _lastSampledFrame = -1;

            GUILayout.FlexibleSpace();

            // clip vs TotalFrames readout (highlight on mismatch)
            var clip = _def.Clip;
            if (clip == null) GUILayout.Label("no clip", EditorStyles.miniLabel);
            else
            {
                int cf = Mathf.RoundToInt(clip.length * clip.frameRate);
                int total = Mathf.Max(1, _def.TotalFrames);
                bool mismatch = cf != total;
                var prev = GUI.contentColor;
                if (mismatch) GUI.contentColor = new Color(1f, 0.85f, 0.3f);
                GUILayout.Label($"clip {clip.frameRate:0}fps {cf}f  \u2194  Total {total}f", EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }
        }
    }

    void EnterPreview() { _lastSampledFrame = -1; Repaint(); SceneView.RepaintAll(); }

    void ExitPreview()
    {
        _playing = false;
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        _lastSampledFrame = -1;
        SceneView.RepaintAll();
    }

    float MapFrameToClipTime(int f)
    {
        var clip = _def.Clip;
        int total = Mathf.Max(1, _def.TotalFrames);
        if (_sampleMode == SampleMode.Normalized)
            return (f / (float)total) * clip.length;
        return Mathf.Clamp(f / Mathf.Max(0.0001f, clip.frameRate), 0f, clip.length);
    }

    void SamplePoseIfNeeded()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!_previewOn || _def == null || _def.Clip == null || _previewTarget == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (_currentFrame == _lastSampledFrame) return;

        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_previewTarget, _def.Clip, MapFrameToClipTime(_currentFrame));
        AnimationMode.EndSampling();

        _lastSampledFrame = _currentFrame;
        SceneView.RepaintAll();
    }

    void OnEditorUpdate()
    {
        if (!_playing || !_previewOn || _def == null) return;
        double now = EditorApplication.timeSinceStartup;
        _playAccum += (now - _lastTick) * _playFps;
        _lastTick = now;

        if (_playAccum >= 1.0)
        {
            int adv = (int)_playAccum;
            _playAccum -= adv;
            int total = Mathf.Max(1, _def.TotalFrames);
            _currentFrame = (_currentFrame + adv) % (total + 1);
            Repaint();
        }
    }

    void BuildLayout()
    {
        float y = RulerH;
        for (int i = 0; i < Lanes.Length; i++)
        {
            var bars = CollectBars(i);
            int rows = PackRows(bars);
            _bars[i] = bars; _rows[i] = rows; _laneY[i] = y;
            _laneH[i] = LanePad * 2 + rows * RowH + (rows - 1) * RowGap;
            y += _laneH[i];
        }
        _lanesTotalH = y - RulerH;
    }

    List<Bar> CollectBars(int lane)
    {
        var list = new List<Bar>();
        switch (lane)
        {
            case 0: if (_def.HitWindows != null) for (int i = 0; i < _def.HitWindows.Length; i++) { var w = _def.HitWindows[i]; list.Add(new Bar { Index = i, Start = w.StartFrame, End = w.EndFrame, Label = w.Damage + "dmg" }); } break;
            case 1: if (_def.CancelWindows != null) for (int i = 0; i < _def.CancelWindows.Length; i++) { var w = _def.CancelWindows[i]; list.Add(new Bar { Index = i, Start = w.StartFrame, End = w.EndFrame, Label = w.AllowedInto.ToString() }); } break;
            case 2: if (_def.InvulnWindows != null) for (int i = 0; i < _def.InvulnWindows.Length; i++) { var w = _def.InvulnWindows[i]; list.Add(new Bar { Index = i, Start = w.StartFrame, End = w.EndFrame, Label = "i-frame" }); } break;
            case 3: if (_def.MotionImpulses != null) for (int i = 0; i < _def.MotionImpulses.Length; i++) { var w = _def.MotionImpulses[i]; list.Add(new Bar { Index = i, Start = w.StartFrame, End = w.EndFrame, Label = w.DrivesVertical ? "V" : "" }); } break;
            case 4: if (_def.RangedFires != null) for (int i = 0; i < _def.RangedFires.Length; i++) { var w = _def.RangedFires[i]; list.Add(new Bar { Index = i, Start = w.Frame, End = w.Frame, Label = w.Mode.ToString() }); } break;
        }
        return list;
    }

    static int PackRows(List<Bar> bars)
    {
        bars.Sort((a, b) => a.Start != b.Start ? a.Start - b.Start : a.End - b.End);
        var rowEnd = new List<int>();
        for (int k = 0; k < bars.Count; k++)
        {
            Bar b = bars[k];
            int placed = -1;
            for (int r = 0; r < rowEnd.Count; r++)
                if (b.Start > rowEnd[r]) { placed = r; rowEnd[r] = b.End; break; }
            if (placed < 0) { rowEnd.Add(b.End); placed = rowEnd.Count - 1; }
            b.Row = placed; bars[k] = b;
        }
        return Mathf.Max(1, rowEnd.Count);
    }

    void DrawTimeline(Rect area)
    {
        int total = Mathf.Max(1, _def.TotalFrames);

        Rect track = new Rect(area.x + LaneLabelW, area.y, area.width - LaneLabelW, area.height);
        Rect ruler = new Rect(track.x, area.y, track.width, RulerH);
        Rect lanesR = new Rect(track.x, area.y + RulerH, track.width, _lanesTotalH);
        Rect interact = new Rect(ruler.x, ruler.y, ruler.width, RulerH + _lanesTotalH);

        HandleScroll(track);
        ClampScroll(track, total);
        HandleMouse(interact, total);

        EditorGUI.DrawRect(new Rect(area.x, area.y, LaneLabelW, RulerH), new Color(0.16f, 0.16f, 0.16f));
        EditorGUI.DrawRect(ruler, new Color(0.18f, 0.18f, 0.18f));
        EditorGUI.DrawRect(lanesR, new Color(0.22f, 0.22f, 0.22f));

        GUI.BeginClip(track);
        DrawRuler(total);
        DrawGrid(track.width, total);
        DrawBars();
        DrawPlayhead();
        GUI.EndClip();

        DrawLaneLabels(area, total);
    }

    float FrameToX(int f) => f * _pixelsPerFrame - _scrollX;
    int XToFrame(float localX) => Mathf.RoundToInt((localX + _scrollX) / _pixelsPerFrame);
    int LabelStep() => _pixelsPerFrame >= 24 ? 1 : _pixelsPerFrame >= 12 ? 5 : 10;
    float LaneRowY(int lane, int row) => _laneY[lane] + LanePad + row * (RowH + RowGap);


    void DrawRuler(int total)
    {
        int step = LabelStep();
        var tick = new Color(1, 1, 1, 0.18f);
        for (int f = 0; f <= total; f++)
        {
            float x = FrameToX(f);
            bool major = (f % step == 0) || f == total;
            float h = major ? RulerH : RulerH * 0.5f;
            EditorGUI.DrawRect(new Rect(x, RulerH - h, 1, h), tick);
            if (major) GUI.Label(new Rect(x + 2, 1, 30, RulerH), f.ToString(), EditorStyles.miniLabel);
        }
    }

    void DrawGrid(float trackWidth, int total)
    {
        var faint = new Color(1, 1, 1, 0.05f);
        var sep = new Color(0, 0, 0, 0.25f);
        int step = LabelStep();
        for (int f = 0; f <= total; f += step)
            EditorGUI.DrawRect(new Rect(FrameToX(f), RulerH, 1, _lanesTotalH), faint);
        for (int i = 0; i < Lanes.Length; i++)
            EditorGUI.DrawRect(new Rect(0, _laneY[i], trackWidth, 1), sep);
        EditorGUI.DrawRect(new Rect(0, RulerH + _lanesTotalH - 1, trackWidth, 1), sep);
    }

    void DrawBars()
    {
        for (int i = 0; i < Lanes.Length; i++)
        {
            Color c = Lanes[i].Color;
            bool point = Lanes[i].Kind == LaneKind.Point;
            foreach (var b in _bars[i])
            {
                float ry = LaneRowY(i, b.Row);
                bool sel = i == _selLane && b.Index == _selIndex;
                if (point) DrawPointMarker(b.Start, ry, c, b.Label, sel);
                else DrawRangeBar(b.Start, b.End, ry, c, b.Label, sel);
            }
        }
    }

    void DrawRangeBar(int start, int end, float y, Color c, string label, bool sel)
    {
        if (end < start) end = start;
        float x0 = FrameToX(start);
        float w = Mathf.Max(2, FrameToX(end) - x0);
        Rect r = new Rect(x0, y, w, RowH);

        bool active = _currentFrame >= start && _currentFrame <= end;
        Color fill = c; fill.a = active ? 0.95f : 0.55f;
        EditorGUI.DrawRect(r, fill);

        Color line = sel ? Color.white : Color.Lerp(c, Color.white, active ? 0.55f : 0.3f);
        float t = sel ? 2f : 1f;
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), line);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), line);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), line);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), line);

        if (sel)
        {
            EditorGUI.DrawRect(new Rect(r.x - 1, r.y + r.height * 0.5f - 4, 3, 8), Color.white);
            EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y + r.height * 0.5f - 4, 3, 8), Color.white);
        }

        if (!string.IsNullOrEmpty(label) && r.width > 26)
        {
            var prev = GUI.color; GUI.color = Color.black;
            GUI.Label(new Rect(r.x + 4, r.y, r.width - 6, r.height), label, EditorStyles.miniLabel);
            GUI.color = prev;
        }
    }

    void DrawPointMarker(int frame, float y, Color c, string label, bool sel)
    {
        float x = FrameToX(frame);
        bool active = _currentFrame == frame;
        Color fill = c; fill.a = active ? 1f : 0.7f;
        EditorGUI.DrawRect(new Rect(x - 4, y, 8, RowH), fill);
        EditorGUI.DrawRect(new Rect(x, y, 1, RowH), sel ? Color.white : Color.Lerp(c, Color.white, 0.5f));
        if (sel)
        {
            EditorGUI.DrawRect(new Rect(x - 5, y, 1, RowH), Color.white);
            EditorGUI.DrawRect(new Rect(x + 4, y, 1, RowH), Color.white);
        }
    }

    void DrawPlayhead()
    {
        float x = FrameToX(_currentFrame);
        var col = new Color(1f, 0.95f, 0.4f, 0.95f);
        EditorGUI.DrawRect(new Rect(x, 0, 1.5f, RulerH + _lanesTotalH), col);
        EditorGUI.DrawRect(new Rect(x - 5, 0, 10, RulerH), col);
    }

    void DrawLaneLabels(Rect area, int total)
    {
        for (int i = 0; i < Lanes.Length; i++)
        {
            Rect r = new Rect(area.x, area.y + _laneY[i], LaneLabelW, _laneH[i]);
            EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.16f));
            EditorGUI.DrawRect(new Rect(r.x + 4, r.y + 5, 10, 10), Lanes[i].Color);
            string name = _rows[i] > 1 ? $"{Lanes[i].Name}  \u00D7{_rows[i]}" : Lanes[i].Name;
            GUI.Label(new Rect(r.x + 20, r.y, r.width - 44, 18), name);
            if (GUI.Button(new Rect(r.xMax - 22, r.y + 2, 18, 16), "+")) AddWindow(i, total);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0, 0, 0, 0.25f));
        }
    }

    void HandleMouse(Rect interact, int total)
    {
        _timelineCtrl = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        switch (e.GetTypeForControl(_timelineCtrl))
        {
            case EventType.MouseDown:
                if (e.button == 0 && interact.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = _timelineCtrl; GUIUtility.keyboardControl = 0;
                    Vector2 local = e.mousePosition - new Vector2(interact.x, interact.y);
                    if (!TryBeginBarDrag(local, total)) { _drag = DragMode.Scrub; _selLane = _selIndex = -1; SeekTo(local.x, total); }
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == _timelineCtrl)
                {
                    Vector2 local = e.mousePosition - new Vector2(interact.x, interact.y);
                    if (_drag == DragMode.Scrub) SeekTo(local.x, total);
                    else if (_drag != DragMode.None) DoBarDrag(local.x, total);
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == _timelineCtrl)
                {
                    if (_drag != DragMode.None && _drag != DragMode.Scrub) { EditorUtility.SetDirty(_def); EnsureSO(); _so.Update(); }
                    _drag = DragMode.None; GUIUtility.hotControl = 0; e.Use();
                }
                break;
        }
    }

    bool TryBeginBarDrag(Vector2 local, int total)
    {
        for (int i = 0; i < Lanes.Length; i++)
        {
            bool point = Lanes[i].Kind == LaneKind.Point;
            foreach (var b in _bars[i])
            {
                float ry = LaneRowY(i, b.Row);
                if (local.y < ry || local.y > ry + RowH) continue;
                float x0 = FrameToX(b.Start), x1 = FrameToX(b.End);

                DragMode mode = DragMode.None;
                if (point) { if (Mathf.Abs(local.x - x0) <= 5f) mode = DragMode.Point; }
                else if (Mathf.Abs(local.x - x0) <= Handle) mode = DragMode.LeftEdge;
                else if (Mathf.Abs(local.x - x1) <= Handle) mode = DragMode.RightEdge;
                else if (local.x >= x0 && local.x <= x1) { mode = DragMode.Body; _grabOffset = XToFrame(local.x) - b.Start; }

                if (mode != DragMode.None)
                {
                    _selLane = i; _selIndex = b.Index; _drag = mode;
                    Undo.RecordObject(_def, "Edit Action Window");
                    return true;
                }
            }
        }
        return false;
    }

    void DoBarDrag(float localX, int total)
    {
        int mf = Mathf.Clamp(XToFrame(localX), 0, total);
        int s = GetStart(_selLane, _selIndex), en = GetEnd(_selLane, _selIndex);
        switch (_drag)
        {
            case DragMode.LeftEdge: SetRange(_selLane, _selIndex, Mathf.Clamp(mf, 0, en), en); break;
            case DragMode.RightEdge: SetRange(_selLane, _selIndex, s, Mathf.Clamp(mf, s, total)); break;
            case DragMode.Body: int w = en - s; int ns = Mathf.Clamp(mf - _grabOffset, 0, total - w); SetRange(_selLane, _selIndex, ns, ns + w); break;
            case DragMode.Point: SetRange(_selLane, _selIndex, mf, mf); break;
        }
        Repaint();
    }

    void SeekTo(float localX, int total) { _currentFrame = Mathf.Clamp(XToFrame(localX), 0, total); Repaint(); }

    void HandleScroll(Rect track)
    {
        Event e = Event.current;
        if (e.type != EventType.ScrollWheel || !track.Contains(e.mousePosition)) return;
        if (e.control || e.command)
        {
            float fc = (e.mousePosition.x - track.x + _scrollX) / _pixelsPerFrame;
            _pixelsPerFrame = Mathf.Clamp(_pixelsPerFrame - e.delta.y, MinPxF, MaxPxF);
            _scrollX = fc * _pixelsPerFrame - (e.mousePosition.x - track.x);
        }
        else _scrollX += e.delta.y * 12f;
        e.Use(); Repaint();
    }

    void ClampScroll(Rect track, int total)
    {
        float contentW = total * _pixelsPerFrame;
        _scrollX = Mathf.Clamp(_scrollX, 0, Mathf.Max(0, contentW - track.width + 24f));
    }

    void HandleKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
        int total = Mathf.Max(1, _def.TotalFrames);
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow: StepFrame(e.shift ? -FrameStepBig : -1); e.Use(); break;
            case KeyCode.RightArrow: StepFrame(e.shift ? FrameStepBig : 1); e.Use(); break;
            case KeyCode.Home: _currentFrame = 0; Repaint(); e.Use(); break;
            case KeyCode.End: _currentFrame = total; Repaint(); e.Use(); break;
            case KeyCode.Space: if (_previewOn) { _playing = !_playing; _lastTick = EditorApplication.timeSinceStartup; _playAccum = 0; } e.Use(); break;
            case KeyCode.Delete:
            case KeyCode.Backspace: if (_selLane >= 0) { DeleteSelected(); e.Use(); } break;
        }
    }

    void StepFrame(int d)
    {
        int total = _def != null ? Mathf.Max(1, _def.TotalFrames) : 1;
        _currentFrame = Mathf.Clamp(_currentFrame + d, 0, total);
        Repaint();
    }

    int GetStart(int lane, int i)
    {
        switch (lane)
        {
            case 0: return _def.HitWindows[i].StartFrame;
            case 1: return _def.CancelWindows[i].StartFrame;
            case 2: return _def.InvulnWindows[i].StartFrame;
            case 3: return _def.MotionImpulses[i].StartFrame;
            case 4: return _def.RangedFires[i].Frame;
        }
        return 0;
    }

    int GetEnd(int lane, int i)
    {
        switch (lane)
        {
            case 0: return _def.HitWindows[i].EndFrame;
            case 1: return _def.CancelWindows[i].EndFrame;
            case 2: return _def.InvulnWindows[i].EndFrame;
            case 3: return _def.MotionImpulses[i].EndFrame;
            case 4: return _def.RangedFires[i].Frame;
        }
        return 0;
    }

    void SetRange(int lane, int i, int s, int e)
    {
        switch (lane)
        {
            case 0: _def.HitWindows[i].StartFrame = s; _def.HitWindows[i].EndFrame = e; break;
            case 1: _def.CancelWindows[i].StartFrame = s; _def.CancelWindows[i].EndFrame = e; break;
            case 2: _def.InvulnWindows[i].StartFrame = s; _def.InvulnWindows[i].EndFrame = e; break;
            case 3: _def.MotionImpulses[i].StartFrame = s; _def.MotionImpulses[i].EndFrame = e; break;
            case 4: _def.RangedFires[i].Frame = s; break;
        }
    }

    void AddWindow(int lane, int total)
    {
        EnsureSO(); _so.Update();
        var arr = _so.FindProperty(Lanes[lane].Prop);
        arr.arraySize++;
        var el = arr.GetArrayElementAtIndex(arr.arraySize - 1);
        if (Lanes[lane].Kind == LaneKind.Point)
            el.FindPropertyRelative("Frame").intValue = _currentFrame;
        else
        {
            el.FindPropertyRelative("StartFrame").intValue = _currentFrame;
            el.FindPropertyRelative("EndFrame").intValue = Mathf.Min(total, _currentFrame + 4);
        }
        _so.ApplyModifiedProperties();
        _selLane = lane; _selIndex = arr.arraySize - 1;
        Repaint();
    }

    void DeleteSelected()
    {
        EnsureSO(); _so.Update();
        var arr = _so.FindProperty(Lanes[_selLane].Prop);
        if (_selIndex >= 0 && _selIndex < arr.arraySize)
            arr.DeleteArrayElementAtIndex(_selIndex);
        _so.ApplyModifiedProperties();
        _selLane = _selIndex = -1;
        Repaint();
    }

    void DrawInspector()
    {
        EditorGUILayout.Space(4);
        if (_selLane < 0 || _selIndex < 0)
        {
            EditorGUILayout.LabelField("Drag edges to resize \u00B7 drag body to move \u00B7 click empty to seek \u00B7 +/Del to add/remove \u00B7 Space to play", EditorStyles.miniLabel);
            return;
        }

        EnsureSO(); _so.Update();
        var arr = _so.FindProperty(Lanes[_selLane].Prop);
        if (arr == null || _selIndex >= arr.arraySize) { _selLane = _selIndex = -1; return; }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{Lanes[_selLane].Name} Window [{_selIndex}]", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete", GUILayout.Width(60))) { DeleteSelected(); return; }
            }
            EditorGUILayout.PropertyField(arr.GetArrayElementAtIndex(_selIndex), includeChildren: true);
        }
        _so.ApplyModifiedProperties();
    }
}