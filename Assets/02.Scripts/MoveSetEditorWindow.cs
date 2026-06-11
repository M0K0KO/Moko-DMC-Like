using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

/// <summary>
/// Moko Move Set Editor - STEP 4 (tree/outliner over a flat MoveSet.Entries).
///
/// 4a: view + select + edit (PropertyField/Undo) + add/delete + priority ¡ã¡å.
/// 4b additions: readable rows (condition badges, dimmed links, tinted groups) and
///     double-click a row ¡æ opens the ActionEditor on that entry's Result.
///
/// PRIORITY = order in MoveSet.Entries (ActionResolver scans top-down, first match wins).
/// Dodge/Swap are hardcoded in ActionResolver (not data), so they are NOT edited here.
///
/// Place under an "Editor" folder. Open: menu "Moko/Move Set Editor" or double-click a MoveSet.
/// </summary>
public class MoveSetEditorWindow : EditorWindow
{
    [SerializeField] MoveSet _moveSet;
    [SerializeField] TreeViewState _treeState;
    [SerializeField] int _selEntry = -1;

    MoveSetTreeView _tree;
    SerializedObject _so;
    Vector2 _detailScroll;

    const float DetailH = 240f;

    [MenuItem("Moko/Move Set Editor")]
    static void Open() => GetWindow<MoveSetEditorWindow>("Move Set");

    [UnityEditor.Callbacks.OnOpenAsset]
    static bool OnOpenAsset(int instanceID, int line)
    {
        var ms = EditorUtility.InstanceIDToObject(instanceID) as MoveSet;
        if (ms == null) return false;
        GetWindow<MoveSetEditorWindow>("Move Set").SetTarget(ms);
        return true;
    }

    void OnEnable()
    {
        _treeState ??= new TreeViewState();
        _tree = new MoveSetTreeView(_treeState, () => _moveSet);
        _tree.Reload();
        _tree.ExpandAll();
    }

    void SetTarget(MoveSet ms)
    {
        _moveSet = ms;
        _so = ms != null ? new SerializedObject(ms) : null;
        _selEntry = -1;
        _tree.Reload();
        _tree.ExpandAll();
        Repaint();
    }

    void EnsureSO()
    {
        if (_moveSet == null) { _so = null; return; }
        if (_so == null || _so.targetObject != _moveSet) _so = new SerializedObject(_moveSet);
    }

    void OnGUI()
    {
        DrawToolbar();

        if (_moveSet == null)
        {
            EditorGUILayout.HelpBox("Select a MoveSet.\n(Double-click the asset, or assign it above.)", MessageType.Info);
            return;
        }

        EnsureSO();
        _so.Update();

        Rect remain = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float detailH = Mathf.Min(DetailH, remain.height * 0.5f);
        Rect treeRect = new Rect(remain.x, remain.y, remain.width, remain.height - detailH);
        Rect detailRect = new Rect(remain.x, remain.yMax - detailH, remain.width, detailH);

        _tree.OnGUI(treeRect);

        var sel = _tree.GetSelection();
        _selEntry = sel.Count > 0 ? _tree.EntryIndexOf(sel[0]) : -1;

        GUILayout.BeginArea(detailRect, EditorStyles.helpBox);
        DrawDetail();
        GUILayout.EndArea();

        _so.ApplyModifiedProperties();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            var ms = (MoveSet)EditorGUILayout.ObjectField(_moveSet, typeof(MoveSet), false, GUILayout.Width(220));
            if (EditorGUI.EndChangeCheck()) SetTarget(ms);

            using (new EditorGUI.DisabledScope(_moveSet == null))
            {
                if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(56))) _tree.ExpandAll();
                if (GUILayout.Button("Collapse", EditorStyles.toolbarButton, GUILayout.Width(62))) _tree.CollapseAll();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", GUILayout.Width(46));
            _tree.searchString = EditorGUILayout.TextField(_tree != null ? _tree.searchString : "", EditorStyles.toolbarSearchField, GUILayout.Width(180));
        }
    }

    // --------------------------------------------------------------- detail pane
    void DrawDetail()
    {
        var arr = _so.FindProperty("Entries");

        if (_selEntry < 0 || _selEntry >= arr.arraySize)
        {
            EditorGUILayout.LabelField("Select a transition to edit, or add a starter.", EditorStyles.miniLabel);
            if (GUILayout.Button("+ Neutral starter", GUILayout.Width(130))) AddEntry(null);
            return;
        }

        var el = arr.GetArrayElementAtIndex(_selEntry);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Entry [{_selEntry}]", EditorStyles.boldLabel, GUILayout.Width(80));

            using (new EditorGUI.DisabledScope(FindSibling(_selEntry, -1) < 0))
                if (GUILayout.Button("\u25B2 Priority", GUILayout.Width(80))) { MovePriority(-1); return; }
            using (new EditorGUI.DisabledScope(FindSibling(_selEntry, +1) < 0))
                if (GUILayout.Button("\u25BC Priority", GUILayout.Width(80))) { MovePriority(+1); return; }

            GUILayout.FlexibleSpace();

            var result = el.FindPropertyRelative("Result").objectReferenceValue as ActionDefinition;
            using (new EditorGUI.DisabledScope(result == null))
                if (GUILayout.Button("+ Follow-up", GUILayout.Width(90))) { AddEntry(result); return; }
            if (GUILayout.Button("+ Starter", GUILayout.Width(74))) { AddEntry(null); return; }
            if (GUILayout.Button("Delete", GUILayout.Width(60))) { DeleteEntry(); return; }
        }

        EditorGUILayout.Space(2);
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
        EditorGUILayout.PropertyField(el, includeChildren: true);
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------ structural ops
    void AddEntry(ActionDefinition fromMove)
    {
        var arr = _so.FindProperty("Entries");
        arr.arraySize++;
        var e = arr.GetArrayElementAtIndex(arr.arraySize - 1);
        e.FindPropertyRelative("FromMove").objectReferenceValue = fromMove;
        e.FindPropertyRelative("Result").objectReferenceValue = null;
        e.FindPropertyRelative("Trigger").enumValueIndex = (int)InputId.Attack;
        e.FindPropertyRelative("Dir").enumValueIndex = (int)DirCondition.Any;
        e.FindPropertyRelative("Loco").enumValueIndex = (int)LocoCondition.Any;
        e.FindPropertyRelative("Lock").enumValueIndex = (int)LockCondition.Any;
        e.FindPropertyRelative("RequireJustSwapped").boolValue = false;
        _so.ApplyModifiedProperties();

        int newIndex = arr.arraySize - 1;
        _tree.Reload();
        _tree.SelectEntry(newIndex);
    }

    void DeleteEntry()
    {
        var arr = _so.FindProperty("Entries");
        if (_selEntry < 0 || _selEntry >= arr.arraySize) return;
        arr.DeleteArrayElementAtIndex(_selEntry);
        _so.ApplyModifiedProperties();
        _selEntry = -1;
        _tree.SetSelection(new List<int>());
        _tree.Reload();
    }

    void MovePriority(int dir)
    {
        int j = FindSibling(_selEntry, dir);
        if (j < 0) return;
        var arr = _so.FindProperty("Entries");
        arr.MoveArrayElement(_selEntry, j);
        _so.ApplyModifiedProperties();
        _tree.Reload();
        _tree.SelectEntry(j);
    }

    int FindSibling(int i, int dir)
    {
        var entries = _moveSet.Entries;
        if (entries == null || i < 0 || i >= entries.Length) return -1;
        var from = entries[i].FromMove;
        for (int j = i + dir; j >= 0 && j < entries.Length; j += dir)
            if (entries[j].FromMove == from) return j;
        return -1;
    }

    // =====================================================================  tree
    class MoveSetTreeView : TreeView
    {
        readonly System.Func<MoveSet> _get;
        readonly Dictionary<int, int> _idToEntry = new();  // treeId -> entry index (-1 = group/link)
        readonly HashSet<int> _linkIds = new();

        static readonly Color ColLoco = new Color(0.30f, 0.55f, 0.90f, 0.90f);
        static readonly Color ColLock = new Color(0.90f, 0.55f, 0.20f, 0.90f);
        static readonly Color ColSwap = new Color(0.58f, 0.42f, 0.82f, 0.90f);
        static readonly Color ColGroup = new Color(1f, 1f, 1f, 0.04f);
        static readonly Color ColDim = new Color(0f, 0f, 0f, 0.35f);

        public MoveSetTreeView(TreeViewState state, System.Func<MoveSet> get) : base(state)
        {
            _get = get;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
        }

        public int EntryIndexOf(int id) => _idToEntry.TryGetValue(id, out var e) ? e : -1;

        public void SelectEntry(int entryIndex)
        {
            foreach (var kv in _idToEntry)
                if (kv.Value == entryIndex)
                {
                    SetSelection(new[] { kv.Key }, TreeViewSelectionOptions.RevealAndFrame);
                    return;
                }
        }

        // double-click a row -> open the ActionEditor on its Result (via the asset's OnOpenAsset)
        protected override void DoubleClickedItem(int id)
        {
            int entry = EntryIndexOf(id);
            var ms = _get();
            if (entry < 0 || ms?.Entries == null || entry >= ms.Entries.Length) return;
            var result = ms.Entries[entry].Result;
            if (result != null) AssetDatabase.OpenAsset(result);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);                       // foldout + label + selection + search highlight
            var item = args.item;

            if (_linkIds.Contains(item.id)) { EditorGUI.DrawRect(args.rowRect, ColDim); return; }

            int entry = EntryIndexOf(item.id);
            if (entry < 0) { EditorGUI.DrawRect(args.rowRect, ColGroup); return; }   // group header

            var ms = _get();
            if (ms?.Entries == null || entry >= ms.Entries.Length) return;
            DrawBadges(args.rowRect, ms.Entries[entry]);
        }

        void DrawBadges(Rect row, MoveEntry e)
        {
            float x = row.xMax - 6f;
            if (e.RequireJustSwapped) Badge(ref x, row, "JS", ColSwap);
            if (e.Lock != LockCondition.Any) Badge(ref x, row, e.Lock == LockCondition.Locked ? "LK" : "UL", ColLock);
            if (e.Loco != LocoCondition.Any) Badge(ref x, row, e.Loco == LocoCondition.Airborne ? "AIR" : "GND", ColLoco);
        }

        static void Badge(ref float x, Rect row, string text, Color c)
        {
            float w = EditorStyles.miniLabel.CalcSize(new GUIContent(text)).x + 8f;
            x -= w;
            var r = new Rect(x, row.y + (row.height - 15f) * 0.5f, w, 15f);
            EditorGUI.DrawRect(r, c);
            var prev = GUI.color; GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 4f, r.y - 1f, r.width - 6f, r.height), text, EditorStyles.miniLabel);
            GUI.color = prev;
            x -= 4f;
        }

        protected override TreeViewItem BuildRoot()
        {
            _idToEntry.Clear();
            _linkIds.Clear();
            int id = 1;
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            var ms = _get();
            var entries = ms != null ? ms.Entries : null;
            if (entries == null || entries.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = id++, displayName = "(no entries - add a neutral starter)" });
                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            var placed = new HashSet<int>();

            var neutral = MakeGroup(ref id, "Neutral (starters)");
            root.AddChild(neutral);
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].FromMove == null)
                    AddEntryNode(neutral, i, ref id, entries, placed);

            for (int i = 0; i < entries.Length; i++)
            {
                if (placed.Contains(i) || entries[i].FromMove == null) continue;
                var from = entries[i].FromMove;
                var grp = MakeGroup(ref id, $"From: {Name(from)}");
                root.AddChild(grp);
                for (int k = 0; k < entries.Length; k++)
                    if (!placed.Contains(k) && entries[k].FromMove == from)
                        AddEntryNode(grp, k, ref id, entries, placed);
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        TreeViewItem MakeGroup(ref int id, string label)
        {
            var item = new TreeViewItem { id = id++, displayName = label };
            _idToEntry[item.id] = -1;
            return item;
        }

        void AddEntryNode(TreeViewItem parent, int i, ref int id, MoveEntry[] entries, HashSet<int> placed)
        {
            bool link = placed.Contains(i);
            var item = new TreeViewItem { id = id++, displayName = Label(entries[i], link) };
            _idToEntry[item.id] = link ? -1 : i;
            if (link) _linkIds.Add(item.id);
            parent.AddChild(item);
            if (link) return;

            placed.Add(i);
            var result = entries[i].Result;
            if (result == null) return;
            for (int k = 0; k < entries.Length; k++)
                if (entries[k].FromMove == result)
                    AddEntryNode(item, k, ref id, entries, placed);
        }

        static string Label(MoveEntry e, bool link)
        {
            string trig = e.Dir == DirCondition.Any ? e.Trigger.ToString() : $"{e.Dir}+{e.Trigger}";
            string res = e.Result != null ? e.Result.name : "(none)";
            string arrow = link ? "\u21AA" : "\u2192";
            return $"{arrow} [{trig}] {res}";
        }

        static string Name(Object o) => o != null ? o.name : "(none)";
    }
}