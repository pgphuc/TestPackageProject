using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BookmarksWindow : EditorWindow
{
    [System.Serializable]
    private class Bookmark
    {
        public string guid;
        public string label;
    }

    private List<Bookmark> _bookmarks = new();
    private Vector2 _scroll;
    private int _renamingIndex = -1;
    private string _renameBuffer = "";

    private const string PrefKey = "BookmarksWindow_Data";

    [MenuItem("Window/Bookmarks")]
    public static void Open()
    {
        GetWindow<BookmarksWindow>("Bookmarks");
    }

    private void OnEnable() => Load();
    private void OnDisable() => Save();

    private void OnGUI()
    {
        DrawToolbar();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _bookmarks.Count; i++)
            DrawItem(i);
        EditorGUILayout.EndScrollView();

        // Drag & drop vào window
        HandleDrop();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Bookmarks", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Clear All", EditorStyles.toolbarButton))
        {
            if (EditorUtility.DisplayDialog("Clear", "Remove all bookmarks?", "Yes", "Cancel"))
                _bookmarks.Clear();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawItem(int i)
    {
        var bm = _bookmarks[i];
        var obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(bm.guid));

        // Scene objects fallback (runtime)
        bool missing = obj == null;

        EditorGUILayout.BeginHorizontal();

        // Icon + ping
        if (!missing)
        {
            var icon = AssetPreview.GetMiniThumbnail(obj) ?? EditorGUIUtility.ObjectContent(obj, obj.GetType()).image as Texture2D;
            if (GUILayout.Button(new GUIContent(icon), GUIStyle.none, GUILayout.Width(18), GUILayout.Height(18)))
                EditorGUIUtility.PingObject(obj);
        }
        else
        {
            GUILayout.Label("?", GUILayout.Width(18));
        }

        // Label / rename
        if (_renamingIndex == i)
        {
            _renameBuffer = EditorGUILayout.TextField(_renameBuffer);
            if (GUILayout.Button("✓", GUILayout.Width(22)))
            {
                bm.label = _renameBuffer;
                _renamingIndex = -1;
            }
        }
        else
        {
            string display = string.IsNullOrEmpty(bm.label)
                ? (missing ? "<missing>" : obj.name)
                : bm.label;

            if (missing) GUI.contentColor = Color.red;
            if (GUILayout.Button(display, EditorStyles.label) && !missing)
                Selection.activeObject = obj;
            GUI.contentColor = Color.white;
        }

        GUILayout.FlexibleSpace();

        // Rename
        if (GUILayout.Button("✎", GUILayout.Width(22)))
        {
            _renamingIndex = i;
            _renameBuffer = bm.label;
        }

        // Remove
        if (GUILayout.Button("✕", GUILayout.Width(22)))
        {
            _bookmarks.RemoveAt(i);
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void HandleDrop()
    {
        var e = Event.current;
        var dropArea = new Rect(0, 0, position.width, position.height);

        if (!dropArea.Contains(e.mousePosition)) return;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Link;

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
                AddObject(obj);
            e.Use();
        }
    }

    private void AddObject(Object obj)
    {
        string path = AssetDatabase.GetAssetPath(obj);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid)) return; // scene object, skip

        if (_bookmarks.Exists(b => b.guid == guid)) return; // no duplicate

        _bookmarks.Add(new Bookmark { guid = guid, label = "" });
    }

    // --- Persistence ---
    private void Save()
    {
        var wrapper = new BookmarkList { items = _bookmarks };
        EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(wrapper));
    }

    private void Load()
    {
        string json = EditorPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            var wrapper = JsonUtility.FromJson<BookmarkList>(json);
            _bookmarks = wrapper?.items ?? new List<Bookmark>();
        }
    }

    [System.Serializable]
    private class BookmarkList { public List<Bookmark> items; }
}