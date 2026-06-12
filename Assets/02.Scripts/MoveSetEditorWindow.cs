using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

/// <summary>
/// Moko Move Set Editor - multi-column inline editor over a flat MoveSet.Entries.
///
/// Edit directly in the rows: Trigger / Dir / Loco / Lock / JS / Result columns.
///   - drag a row onto another row/group to REPARENT (FromMove), or between rows to REORDER
///     (priority = order in Entries among siblings with the same FromMove)
///   - right-click a row for actions (add follow-up / starter / duplicate / delete / priority / open)
///   - double-click ¡æ open the Result in the ActionEditor
///   - left color stripe per row by Trigger; rows whose Result is open in the ActionEditor are highlighted
///
/// Data model is unchanged (flat MoveEntry[] edited via SerializedProperty -> automatic Undo).
/// Dodge/Swap are hardcoded in ActionResolver (not data), so they are NOT edited here.
///
/// Place under an "Editor" folder. Open: menu "Moko/Move Set Editor" or double-click a MoveSet.
/// </summary>
public class MoveSetEditorWindow : EditorWindow
{
    [SerializeField] MoveSet _moveSet;
    [SerializeField] TreeViewState _treeState;
    [SerializeField] MultiColumnHeaderState _headerState;

    MoveSetTreeView _tree;
    SerializedObject _so;

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

        bool firstInit = _headerState == null;
        var newHeader = CreateHeaderState();
        if (_headerState != null && MultiColumnHeaderState.CanOverwriteSerializedFields(_headerState, newHeader))
            MultiColumnHeaderState.OverwriteSerializedFields(_headerState, newHeader);
        _headerState = newHeader;

        var header = new MultiColumnHeader(_headerState) { canSort = false };
        if (firstInit) header.ResizeToFit();

        _tree = new MoveSetTreeView(_treeState, header, () => _so, () => _moveSet)
        {
            OnReparent = Reparent,
            OnContext = HandleContext,
        };
        _tree.Reload();
        _tree.ExpandAll();
    }

    // repaint while open so the ActionEditor-driven highlight stays roughly live
    void OnInspectorUpdate() { if (_moveSet != null) Repaint(); }

    void SetTarget(MoveSet ms)
    {
        _moveSet = ms;
        _so = ms != null ? new SerializedObject(ms) : null;
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

        EditorGUILayout.LabelField("Right-click: actions  \u00B7  Drag: reparent / reorder  \u00B7  Double-click: open in ActionEditor", EditorStyles.miniLabel);

        EnsureSO();
        _so.Update();

        Rect treeRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _tree.OnGUI(treeRect);

        if (_tree.ConsumeReload()) _so.ApplyModifiedProperties();   // commit Result change before rebuild
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
                if (GUILayout.Button("+ Starter", EditorStyles.toolbarButton, GUILayout.Width(70))) AddEntry(null);
                if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(56))) _tree.ExpandAll();
                if (GUILayout.Button("Collapse", EditorStyles.toolbarButton, GUILayout.Width(62))) _tree.CollapseAll();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", GUILayout.Width(46));
            _tree.searchString = EditorGUILayout.TextField(_tree != null ? _tree.searchString : "", EditorStyles.toolbarSearchField, GUILayout.Width(180));
        }
    }

    // ------------------------------------------------------------- header cols
    static MultiColumnHeaderState CreateHeaderState()
    {
        var cols = new[]
        {
            Col("Move",    260, 160, true,  TextAlignment.Left),
            Col("Trigger",  95,  60, false, TextAlignment.Left),
            Col("Dir",      80,  50, false, TextAlignment.Left),
            Col("Loco",     85,  55, false, TextAlignment.Left),
            Col("Lock",     85,  55, false, TextAlignment.Left),
            Col("JS",       32,  28, false, TextAlignment.Center),
            Col("Result",  160,  90, true,  TextAlignment.Left),
        };
        return new MultiColumnHeaderState(cols);
    }

    static MultiColumnHeaderState.Column Col(string title, float w, float min, bool autoResize, TextAlignment align) =>
        new MultiColumnHeaderState.Column
        {
            headerContent = new GUIContent(title),
            headerTextAlignment = align,
            width = w,
            minWidth = min,
            autoResize = autoResize,
            canSort = false,
            allowToggleVisibility = false,
        };

    // ------------------------------------------------------------ structural ops
    void AddEntry(ActionDefinition fromMove)
    {
        EnsureSO(); _so.Update();
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

        int idx = arr.arraySize - 1;
        _tree.Reload();
        _tree.SelectEntry(idx);
    }

    void DuplicateEntry(int idx)
    {
        EnsureSO(); _so.Update();
        var arr = _so.FindProperty("Entries");
        if (idx < 0 || idx >= arr.arraySize) return;
        arr.InsertArrayElementAtIndex(idx);   // arr[idx] becomes a copy; original shifts to idx+1
        _so.ApplyModifiedProperties();
        _tree.Reload();
        _tree.SelectEntry(idx);
    }

    void DeleteEntry(int idx)
    {
        EnsureSO(); _so.Update();
        var arr = _so.FindProperty("Entries");
        if (idx < 0 || idx >= arr.arraySize) return;
        arr.DeleteArrayElementAtIndex(idx);
        _so.ApplyModifiedProperties();
        _tree.SetSelection(new List<int>());
        _tree.Reload();
    }

    void MovePriority(int idx, int dir)
    {
        int j = FindSibling(idx, dir);
        if (j < 0) return;
        EnsureSO(); _so.Update();
        var arr = _so.FindProperty("Entries");
        arr.MoveArrayElement(idx, j);
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

    void HandleContext(string op, int entry)
    {
        var entries = _moveSet.Entries;
        switch (op)
        {
            case "followup": AddEntry(entry >= 0 && entries != null && entry < entries.Length ? entries[entry].Result : null); break;
            case "starter": AddEntry(null); break;
            case "dup": DuplicateEntry(entry); break;
            case "del": DeleteEntry(entry); break;
            case "raise": MovePriority(entry, -1); break;
            case "lower": MovePriority(entry, +1); break;
        }
    }

    // drag-drop: reparent `dragged` under `newParent` (null = neutral), place at sibling slot `childIndex`.
    void Reparent(int dragged, ActionDefinition newParent, int childIndex)
    {
        EnsureSO(); _so.Update();
        var arr = _so.FindProperty("Entries");
        if (dragged < 0 || dragged >= arr.arraySize) return;

        Undo.RecordObject(_moveSet, "Reparent Move Entry");
        arr.GetArrayElementAtIndex(dragged).FindPropertyRelative("FromMove").objectReferenceValue = newParent;
        _so.ApplyModifiedProperties();

        var entries = _moveSet.Entries;
        var siblings = new List<int>();
        for (int k = 0; k < entries.Length; k++)
            if (k != dragged && entries[k].FromMove == newParent) siblings.Add(k);

        int target = (childIndex < 0 || childIndex >= siblings.Count)
            ? (siblings.Count > 0 ? siblings[siblings.Count - 1] + 1 : dragged)
            : siblings[childIndex];

        int dst = Mathf.Clamp(dragged < target ? target - 1 : target, 0, arr.arraySize - 1);
        arr.MoveArrayElement(dragged, dst);
        _so.ApplyModifiedProperties();

        _tree.Reload();
        _tree.SelectEntry(dst);
        Repaint();
    }

    // =====================================================================  tree
    class MoveSetTreeView : TreeView
    {
        readonly System.Func<MoveSet> _get;
        readonly System.Func<SerializedObject> _getSO;
        readonly Dictionary<int, int> _idToEntry = new();                  // treeId -> entry index (-1 = group/link)
        readonly HashSet<int> _linkIds = new();
        readonly Dictionary<int, ActionDefinition> _parentMoveOf = new();  // valid drop target -> FromMove for its children
        readonly HashSet<int> _dropParentIds = new();
        bool _reload;

        public System.Action<int, ActionDefinition, int> OnReparent;       // (draggedEntry, newParentMove, childIndex)
        public System.Action<string, int> OnContext;                       // (op, entry)

        static readonly Color[] TrigPalette =
        {
            new Color(0.90f, 0.32f, 0.28f), new Color(0.30f, 0.68f, 0.90f), new Color(0.45f, 0.80f, 0.45f),
            new Color(0.92f, 0.72f, 0.26f), new Color(0.70f, 0.45f, 0.90f), new Color(0.90f, 0.50f, 0.72f),
            new Color(0.48f, 0.78f, 0.74f),
        };
        static readonly Color ColGroup = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color ColHi = new Color(1f, 0.92f, 0.30f, 0.13f);
        static readonly Color ColHiBar = new Color(1f, 0.88f, 0.20f, 0.95f);

        public MoveSetTreeView(TreeViewState state, MultiColumnHeader header, System.Func<SerializedObject> getSO, System.Func<MoveSet> get)
            : base(state, header)
        {
            _get = get;
            _getSO = getSO;
            rowHeight = 20f;
            columnIndexForTreeFoldouts = 0;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
        }

        public bool ConsumeReload() { bool r = _reload; _reload = false; return r; }
        public int EntryIndexOf(int id) => _idToEntry.TryGetValue(id, out var e) ? e : -1;

        public void SelectEntry(int entryIndex)
        {
            foreach (var kv in _idToEntry)
                if (kv.Value == entryIndex) { SetSelection(new[] { kv.Key }, TreeViewSelectionOptions.RevealAndFrame); return; }
        }

        protected override void DoubleClickedItem(int id)
        {
            int entry = EntryIndexOf(id);
            var ms = _get();
            if (entry < 0 || ms?.Entries == null || entry >= ms.Entries.Length) return;
            var result = ms.Entries[entry].Result;
            if (result != null) AssetDatabase.OpenAsset(result);
        }

        protected override void ContextClickedItem(int id)
        {
            int entry = EntryIndexOf(id);
            var menu = new GenericMenu();
            if (entry >= 0)
            {
                menu.AddItem(new GUIContent("Add Follow-up"), false, () => OnContext?.Invoke("followup", entry));
                menu.AddItem(new GUIContent("Duplicate"), false, () => OnContext?.Invoke("dup", entry));
                menu.AddItem(new GUIContent("Delete"), false, () => OnContext?.Invoke("del", entry));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Raise Priority"), false, () => OnContext?.Invoke("raise", entry));
                menu.AddItem(new GUIContent("Lower Priority"), false, () => OnContext?.Invoke("lower", entry));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Open in ActionEditor"), false, () =>
                {
                    var ms = _get();
                    if (ms?.Entries != null && entry < ms.Entries.Length && ms.Entries[entry].Result != null)
                        AssetDatabase.OpenAsset(ms.Entries[entry].Result);
                });
            }
            menu.AddItem(new GUIContent("Add Starter"), false, () => OnContext?.Invoke("starter", entry));
            menu.ShowAsContext();
        }

        // ---- rows / cells ----
        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item;
            int entry = EntryIndexOf(item.id);
            bool isLink = _linkIds.Contains(item.id);
            bool isGroup = entry < 0 && !isLink;

            if (isGroup) EditorGUI.DrawRect(args.rowRect, ColGroup);

            for (int v = 0; v < args.GetNumVisibleColumns(); v++)
                CellGUI(args.GetCellRect(v), args.GetColumn(v), item, entry, isGroup, isLink);

            // cross-tool highlight (drawn last; translucent, does not block the cell controls)
            if (entry >= 0)
            {
                var ms = _get();
                if (ms?.Entries != null && entry < ms.Entries.Length &&
                    ms.Entries[entry].Result != null && ms.Entries[entry].Result == ActionEditorWindow.Current)
                {
                    EditorGUI.DrawRect(args.rowRect, ColHi);
                    EditorGUI.DrawRect(new Rect(args.rowRect.x, args.rowRect.y, 2f, args.rowRect.height), ColHiBar);
                }
            }
        }

        void CellGUI(Rect cell, int col, TreeViewItem item, int entry, bool isGroup, bool isLink)
        {
            if (col == 0) { Col0(cell, item, entry, isGroup, isLink); return; }
            if (entry < 0 || isLink) return;   // groups/links have no editable cells

            var so = _getSO();
            if (so == null) return;
            var el = so.FindProperty("Entries").GetArrayElementAtIndex(entry);
            Rect line = Line(cell);

            switch (col)
            {
                case 1: EditorGUI.PropertyField(line, el.FindPropertyRelative("Trigger"), GUIContent.none); break;
                case 2: EditorGUI.PropertyField(line, el.FindPropertyRelative("Dir"), GUIContent.none); break;
                case 3: EditorGUI.PropertyField(line, el.FindPropertyRelative("Loco"), GUIContent.none); break;
                case 4: EditorGUI.PropertyField(line, el.FindPropertyRelative("Lock"), GUIContent.none); break;
                case 5:
                    var tr = new Rect(cell.x + cell.width * 0.5f - 8f, line.y, 16f, line.height);
                    EditorGUI.PropertyField(tr, el.FindPropertyRelative("RequireJustSwapped"), GUIContent.none);
                    break;
                case 6:
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(line, el.FindPropertyRelative("Result"), GUIContent.none);
                    if (EditorGUI.EndChangeCheck()) _reload = true;   // Result change alters the tree structure
                    break;
            }
        }

        void Col0(Rect cell, TreeViewItem item, int entry, bool isGroup, bool isLink)
        {
            float indent = GetContentIndent(item);

            if (item.hasChildren)
            {
                var fr = new Rect(cell.x + indent - 14f, cell.y, 14f, cell.height);
                bool ex = IsExpanded(item.id);
                bool now = EditorGUI.Foldout(fr, ex, GUIContent.none);
                if (now != ex) SetExpanded(item.id, now);
            }

            var labelRect = new Rect(cell.x + indent, cell.y, cell.width - indent, cell.height);

            if (isGroup) { GUI.Label(labelRect, item.displayName, EditorStyles.boldLabel); return; }

            if (entry >= 0)
            {
                var ms = _get();
                if (ms?.Entries != null && entry < ms.Entries.Length)
                    EditorGUI.DrawRect(new Rect(cell.x + 1f, cell.y + 2f, 3f, cell.height - 4f), TrigColor(ms.Entries[entry].Trigger));
            }

            var prev = GUI.color;
            if (isLink) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(labelRect, item.displayName, isLink ? EditorStyles.miniLabel : EditorStyles.label);
            GUI.color = prev;
        }

        static Rect Line(Rect cell)
        {
            float h = EditorGUIUtility.singleLineHeight;
            return new Rect(cell.x + 1f, cell.y + (cell.height - h) * 0.5f, cell.width - 2f, h);
        }

        static Color TrigColor(InputId t) => TrigPalette[(((int)t % TrigPalette.Length) + TrigPalette.Length) % TrigPalette.Length];

        // ---- drag & drop ----
        protected override bool CanStartDrag(CanStartDragArgs args) => args.draggedItem != null && EntryIndexOf(args.draggedItem.id) >= 0;

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            var ids = SortItemIDsInRowOrder(args.draggedItemIDs);
            if (ids.Count == 0) return;
            int entry = EntryIndexOf(ids[0]);
            if (entry < 0) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("MoveEntryIndex", entry);
            DragAndDrop.objectReferences = new Object[0];
            DragAndDrop.StartDrag("Move Entry");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (DragAndDrop.GetGenericData("MoveEntryIndex") is not int dragged) return DragAndDropVisualMode.None;

            var parent = args.parentItem;
            if (parent == null || !_dropParentIds.Contains(parent.id)) return DragAndDropVisualMode.None;
            if (EntryIndexOf(parent.id) == dragged) return DragAndDropVisualMode.None;

            if (args.performDrop)
            {
                var newParent = _parentMoveOf.TryGetValue(parent.id, out var pm) ? pm : null;
                OnReparent?.Invoke(dragged, newParent, args.insertAtIndex);
            }
            return DragAndDropVisualMode.Move;
        }

        // ---- build ----
        protected override TreeViewItem BuildRoot()
        {
            _idToEntry.Clear();
            _linkIds.Clear();
            _parentMoveOf.Clear();
            _dropParentIds.Clear();
            int id = 1;
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };

            var ms = _get();
            var entries = ms != null ? ms.Entries : null;
            if (entries == null || entries.Length == 0)
            {
                root.AddChild(new TreeViewItem { id = id++, displayName = "(no entries - add a starter)" });
                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            var placed = new HashSet<int>();

            var neutral = MakeGroup(ref id, "Neutral (starters)", null);
            root.AddChild(neutral);
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].FromMove == null)
                    AddEntryNode(neutral, i, ref id, entries, placed);

            for (int i = 0; i < entries.Length; i++)
            {
                if (placed.Contains(i) || entries[i].FromMove == null) continue;
                var from = entries[i].FromMove;
                var grp = MakeGroup(ref id, $"From: {Name(from)}", from);
                root.AddChild(grp);
                for (int k = 0; k < entries.Length; k++)
                    if (!placed.Contains(k) && entries[k].FromMove == from)
                        AddEntryNode(grp, k, ref id, entries, placed);
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        TreeViewItem MakeGroup(ref int id, string label, ActionDefinition parentMove)
        {
            var item = new TreeViewItem { id = id++, displayName = label };
            _idToEntry[item.id] = -1;
            _dropParentIds.Add(item.id);
            _parentMoveOf[item.id] = parentMove;
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

            var result = entries[i].Result;
            if (result != null) { _dropParentIds.Add(item.id); _parentMoveOf[item.id] = result; }

            placed.Add(i);
            if (result == null) return;
            for (int k = 0; k < entries.Length; k++)
                if (entries[k].FromMove == result)
                    AddEntryNode(item, k, ref id, entries, placed);
        }

        static string Label(MoveEntry e, bool link)
        {
            string res = e.Result != null ? e.Result.name : "(none)";
            return $"{(link ? "\u21AA" : "\u2192")} {res}{(link ? "  (link)" : "")}";
        }

        static string Name(Object o) => o != null ? o.name : "(none)";
    }
}