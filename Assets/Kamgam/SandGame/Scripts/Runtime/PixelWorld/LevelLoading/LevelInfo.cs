using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{

    [System.Serializable]
    public class SceneAssetReference : AssetReference
    {
        public SceneAssetReference(string guid) : base(guid) { }

        public override bool ValidateAsset(string path)
        {
            return path.EndsWith(".unity");
        }
    }

    [CreateAssetMenu(fileName = "Level", menuName = "Sand Game Template/Level Info")]
    public partial class LevelInfo : ScriptableObject
    {
        public int Level = 1;

        public SceneAssetReference Scene;

        [SerializeField]
        [Tooltip("Level materials lookup table.")]
        public PixelMaterials Materials;

        [SerializeField]
        protected List<LevelPart> _parts;

#if UNITY_EDITOR
        public List<LevelPart> _EditorImages => _parts;
#endif

        public bool HasScene()
        {
            return Scene != null && Scene.RuntimeKeyIsValid();
        }

        public bool IsPartLoaded(int x, int y)
        {
            var part = GetPart(x, y);
            return part != null && part.LoadComplete;
        }

        public LevelPart GetPart(int x, int y)
        {
            var part = _parts.FirstOrDefault(c => c.Coordinates.x == x && c.Coordinates.y == y);

            if (part != null)
            {
                part.Level = this;
            }

            return part;
        }

        public void CreateNativeMaterialsIfNeeded()
        {
            Materials?.CreateNativeMaterialsIfNeeded();
        }

        public bool HasValidMaterials()
        {
            return Materials != null && Materials.NativeMaterials.IsCreated;
        }

        public void Unload()
        {
            foreach (var part in _parts)
            {
                part.Unload();
            }

            Materials?.Unload();
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (Materials != null && Materials.EnsureValidMaterials())
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
