#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{
    [UnityEditor.CustomEditor(typeof(LevelInfo))]
    public class LevelInfoEditor : UnityEditor.Editor
    {
        public static string AddressableGroupName = "Sand Game Assets";

        LevelInfo obj;

        public void OnEnable()
        {
            obj = target as LevelInfo;
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Import Level Parts"))
            {
                EditorUtility.DisplayProgressBar("Importing level parts", "Working ..", 0.1f);
                reimportParts();
                EditorUtility.ClearProgressBar();
            }

            base.OnInspectorGUI();
        }

        private void reimportParts()
        {
            obj._EditorImages.Clear();

            // Make sure the info object itself  is also addressable
            var objGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
            makeAddressable(objGUID, address: LevelInfo.GetAddressablePath(obj.Level));

            // If a scene is linked then make sure the scene is addressable.
            if (!string.IsNullOrEmpty(obj.Scene.AssetGUID))
            {
                makeAddressable(obj.Scene.AssetGUID);
            }

            // Handle parts
            var path = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(obj));
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    char slash = System.IO.Path.DirectorySeparatorChar;
                    string imagePath = path + slash + "Parts" + slash + LevelPart.GetLevelPartImageFilename(obj.Level, x, y);
                    var guid = AssetDatabase.AssetPathToGUID(imagePath);

                    if (System.IO.File.Exists(imagePath) && !string.IsNullOrEmpty(guid))
                    {

                        // Make image uncrompressed, enable read/write and set to default type.
                        TextureImporter textureImporter = AssetImporter.GetAtPath(imagePath) as TextureImporter;

                        if (textureImporter != null)
                        {
                            textureImporter.isReadable = true;
                            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                            textureImporter.textureType = TextureImporterType.Default;
                            textureImporter.alphaIsTransparency = true;
                            textureImporter.mipmapEnabled = false;
                            textureImporter.filterMode = FilterMode.Point;
                            textureImporter.npotScale = TextureImporterNPOTScale.None;

                            TextureImporterPlatformSettings texset = textureImporter.GetDefaultPlatformTextureSettings();
                            texset.format = TextureImporterFormat.RGBA32;
                            texset.maxTextureSize = 2048;
                            textureImporter.SetPlatformTextureSettings(texset);

                            AssetDatabase.ImportAsset(imagePath);
                        }
                        else
                        {
                            Debug.LogWarning("Texture importer is null for asset: " + imagePath);
                        }

                        // Make image addressable (if it is not addressable already)
                        makeAddressable(guid);

                        // Make image addressable (if it is not addressable already)
                        makeAddressable(guid);

                        // Create part
                        var part = new LevelPart();
                        part.Coordinates.x = x;
                        part.Coordinates.y = y;
                        part.ImageReference = new AssetReference(guid);
                        obj._EditorImages.Add(part);
                    }
                }
            }

            EditorUtility.SetDirty(obj);
            AssetDatabase.SaveAssetIfDirty(obj);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void makeAddressable(string guid, string address = null, string label = null)
        {
            if (!UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                Debug.LogWarning("Addressable Settings don't exist, creating new ones.");

                UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings =
                    UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.groups.Find(a => a.Name == AddressableGroupName);
            // Create group if needed
            if (group == null)
            { 
                group = new AddressableAssetGroup();
                group.Name = AddressableGroupName;
                settings.groups.Add(group);
            }
            // Fall back on default group
            if (group == null)
                group = settings.DefaultGroup;

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            }

            if (string.IsNullOrEmpty(address))
                entry.address = AssetDatabase.GUIDToAssetPath(guid);
            else
                entry.address = address;

            if (!string.IsNullOrEmpty(label))
                entry.labels.Add(label);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryAdded, entry, postEvent: true);
        }
    }
}
#endif
