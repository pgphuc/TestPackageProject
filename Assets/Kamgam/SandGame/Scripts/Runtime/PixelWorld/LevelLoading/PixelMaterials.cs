using Unity.Collections;
using UnityEngine;

namespace Kamgam.SandGame
{
    [CreateAssetMenu(fileName = "PixelMaterials", menuName = "Sand Game Template/Pixel Materials")]
    public partial class PixelMaterials : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Materials table for a level.")]
        protected PixelMaterial[] _materials;
        public PixelMaterial[] Materials { get => _materials; set => _materials = value; }

        [System.NonSerialized]
        public NativeArray<PixelMaterial> NativeMaterials;

        public static PixelMaterial GetDefaultMaterial()
        {
            var material = new PixelMaterial();

            material.density = 1.3f;
            material.startHealth = 100f;
            material.ignitionTemperature = 100f;

            return material;
        }

        public PixelMaterial GetById(PixelMaterialId id)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i].id == id)
                    return _materials[i];
            }

            return _materials[0];
        }

        public int GetIndex(PixelMaterialId id)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i].id == id)
                    return i;
            }

            return -1;
        }

        public void CreateNativeMaterialsIfNeeded()
        {
            // Ensure there is at least one material.
            EnsureValidMaterials();

            if (!NativeMaterials.IsCreated)
            {
                NativeMaterials = new NativeArray<PixelMaterial>(_materials, Allocator.Persistent);
            }
        }

        public void Unload()
        {
            if (NativeMaterials.IsCreated)
                NativeMaterials.Dispose();
        }

        public bool EnsureValidMaterials()
        {
            if (_materials == null || _materials.Length == 0)
            {
                _materials = new PixelMaterial[]
                {
                    GetDefaultMaterial()
                };
                return true;
            }

            // make sure the first material is one without any behaviours
            if (_materials[0].behaviour0 != PixelBehaviour.None)
            {
                var tmp = new PixelMaterial[_materials.Length + 1];
                tmp[0] = GetDefaultMaterial();
                for (int i = 0; i < _materials.Length; i++)
                {
                    tmp[i + 1] = _materials[i];
                }

                _materials = tmp;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (EnsureValidMaterials())
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }

            // Ensure that the native array updates if materials are changed.
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Unload();
            }
        }
#endif
    }
}
