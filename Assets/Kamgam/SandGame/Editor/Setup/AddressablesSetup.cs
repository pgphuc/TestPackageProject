#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kamgam.SandGame
{
    public static class AddressabelesSetup
    {
        [MenuItem("Tools/Sand Game/Debug/Setup Addressable Group")]
        public static void AddGroup()
        {
            var guids = AssetDatabase.FindAssets("t:AddressableAssetGroup");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Sand Game Assets"))
                {
                    var group = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(path);

                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    var existingGroup = settings.groups.Find(a => a != null && a.Name == LevelInfoEditor.AddressableGroupName);
                    // Add if needed
                    if (existingGroup == null)
                    {
                        Debug.Log("Sand Game Installer: Adding Addressable Group " + group.Name);
                        settings.groups.Add(group);
                        settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, postEvent: true, settingsModified: true);
                    }
                }
            }
        }
    }
}
#endif