#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kamgam.SandGame
{
    public static class MaterialShaderFixer
    {
        public class MaterialInfo
        {
            public enum ShaderType { Default, Unlit, Particle }
            public ShaderType Shader = ShaderType.Default;
            public Color Color;
            protected Texture _mainTexture;
            public Texture MainTexture
            {
                get
                {
                    if (_mainTexture == null)
                    {
                        _mainTexture = loadMainTextureFromGUID();
                    }

                    return _mainTexture;
                }

                set => _mainTexture = value;
            }
            public string MainTextureGUID;
            public Vector2Int Tiling;

            public MaterialInfo(ShaderType shader, Color color, string mainTextureGUID, Vector2Int tiling)
            {
                Shader = shader;
                Color = color;
                MainTextureGUID = mainTextureGUID;
                Tiling = tiling;
            }

            public MaterialInfo(ShaderType shader, Color color, string mainTextureGUID)
            {
                Shader = shader;
                Color = color;
                MainTextureGUID = mainTextureGUID;
                Tiling = Vector2Int.one;
            }

            public MaterialInfo(ShaderType shader, Color color, Texture mainTexture, Vector2Int tiling)
            {
                Shader = shader;
                Color = color;
                MainTexture = mainTexture;
                Tiling = tiling;
            }

            public MaterialInfo(ShaderType shader, Color color, Texture mainTexture)
            {
                Shader = shader;
                Color = color;
                MainTexture = mainTexture;
                Tiling = Vector2Int.one;
            }

            public MaterialInfo(ShaderType shader, Color color)
            {
                Shader = shader;
                Color = color;
                MainTexture = null;
                Tiling = Vector2Int.one;
            }

            public Texture loadMainTextureFromGUID()
            {
                if (string.IsNullOrEmpty(MainTextureGUID))
                    return null;

                var path = AssetDatabase.GUIDToAssetPath(new GUID(MainTextureGUID));
                return AssetDatabase.LoadAssetAtPath<Texture>(path);
            }
        }

        public enum RenderPiplelineType
        {
            URP, HDRP, BuiltIn
        }

        static System.Action _onComplete;

        #region StartFixMaterial delayed
        static double startFixingAt;

        public static void FixMaterialsDelayed(System.Action onComplete)
        {
            // Materials may not be loaded at this time. Thus we wait for them to be imported.
            _onComplete = onComplete;
            EditorApplication.update -= onEditorUpdate;
            EditorApplication.update += onEditorUpdate;
            startFixingAt = EditorApplication.timeSinceStartup + 3; // wait N seconds
        }

        static void onEditorUpdate()
        {
            // wait for the time to reach startPackageImportAt
            if (startFixingAt - EditorApplication.timeSinceStartup < 0)
            {
                EditorApplication.update -= onEditorUpdate;
                try
                {
                    FixMaterials();
                }
                finally
                {
                    _onComplete?.Invoke();
                }
                return;
            }
        }
        #endregion


        // Right click copy GUID
        [MenuItem("Assets/Copy GUID")]
        public static void CopyGUID()
        {
            string assetGUIDs = "";
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                var obj = Selection.objects[i];
                string assetPath = AssetDatabase.GetAssetPath(obj);
                assetGUIDs += (i > 0 ? "," : "") + AssetDatabase.AssetPathToGUID(assetPath);
            }

            EditorGUIUtility.systemCopyBuffer = assetGUIDs;
            Debug.Log($"GUID(s) copied to clipboard: {assetGUIDs}");
        }

        [MenuItem("Assets/Copy GUID", true)]
        public static bool ValidateCopyGUID()
        {
            return Selection.objects.Length > 0;
        }

        [MenuItem("Tools/Sand Game/Debug/Fix Materials")]
        public static void FixMaterials()
        {
            RenderPiplelineType createdForRenderPipleline = RenderPiplelineType.BuiltIn;
            var currentRenderPipline = GetCurrentRenderPiplelineType();

            Debug.Log("Upgrading materials from " + createdForRenderPipleline + " to " + currentRenderPipline);

            // Revert to the standard shader of each render pipeline and apply the color.
            if (currentRenderPipline != createdForRenderPipleline)
            {
                // Get the default shader for the currently used render pipeline asset
                var materialPaths = new Dictionary<string, MaterialInfo>
                {
                     { "Assets/Kamgam/SandGame/Materials/Background.mat", 
                        new MaterialInfo( MaterialInfo.ShaderType.Unlit, HexToColor("FFFFFF"), "3e174b79d55c00a4fbcb3f2f494267c4" ) }

                    ,{ "Assets/Kamgam/SandGame/Materials/PixelCanvas.mat", 
                        new MaterialInfo( MaterialInfo.ShaderType.Unlit, HexToColor("FFFFFF") ) }

                    ,{ "Assets/Kamgam/SandGame/Materials/SandGameDefaultMaterial.mat", 
                        new MaterialInfo( MaterialInfo.ShaderType.Default, HexToColor("FFFFFF") ) }
                };

                foreach (var kv in materialPaths)
                {
                    var path = kv.Key;
                    var info = kv.Value;
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

                    Shader shader = null;
                    switch (info.Shader)
                    {
                        case MaterialInfo.ShaderType.Default:
                            shader = GetDefaultShader();
                            break;
                        case MaterialInfo.ShaderType.Unlit:
                            shader = GetDefaultUnlitShader();
                            break;
                        case MaterialInfo.ShaderType.Particle:
                            shader = GetDefaultParticleShader();
                            break;
                        default:
                            break;
                    }
                    if (shader != null)
                    {
                        if (material != null)
                        {
                            Debug.Log($"Setting material '{path}' to shader: " + shader.name);
                            material.shader = shader;
                            material.color = info.Color;
                            if (info.MainTexture != null)
                            {
                                if (material.HasTexture("_Main"))
                                    material.SetTexture("_Main", info.MainTexture);

                                if (material.HasTexture("_MainTex"))
                                    material.SetTexture("_MainTex", info.MainTexture);

                                if (material.HasTexture("_BaseMap"))
                                    material.SetTexture("_BaseMap", info.MainTexture);
                                
                                if (material.HasTexture("_UnlitColorMap"))
                                    material.SetTexture("_UnlitColorMap", info.MainTexture);
                            }
                            material.mainTextureScale = info.Tiling;
                        }
                    }
                    else
                    {
                        Debug.LogError("No default shader found! Please contact support.");
                    }
                }
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.Log("All good, no material to fix.");
            }
        }

        public static Color HexToColor(string hex)
        {
            if (hex[0] == '#')
            {
                hex = hex.Substring(1);
            }

            Color color = new Color();

            if (hex.Length == 6)
            {
                color.r = (byte)System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                color.g = (byte)System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                color.b = (byte)System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                color.a = 1f;
            }
            else if (hex.Length == 8)
            {
                color.r = (byte)System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                color.g = (byte)System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                color.b = (byte)System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                color.a = (byte)System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
            }
            else
            {
                Debug.LogError("Invalid hex color format. Please provide a 6 or 8 character hex color string.");
            }

            return color;
        }

        public static RenderPiplelineType GetCurrentRenderPiplelineType()
        {
            // Assume URP as default
            var renderPipeline = RenderPiplelineType.URP;

            // check if Standard or HDRP
            if (getUsedRenderPipeline() == null)
                renderPipeline = RenderPiplelineType.BuiltIn; // Standard
            else if (!getUsedRenderPipeline().GetType().Name.Contains("Universal"))
                renderPipeline = RenderPiplelineType.HDRP; // HDRP

            return renderPipeline;
        }

        public static Shader GetDefaultShader()
        {
            if (getUsedRenderPipeline() == null)
                return Shader.Find("Standard");
            else
                return getUsedRenderPipeline().defaultShader;
        }

        public static Shader GetDefaultUnlitShader()
        {
            Shader shader = null;
            if (getUsedRenderPipeline() == null)
                return Shader.Find("Unlit/Texture");
            else
            {
                if (getUsedRenderPipeline().name.Contains("Universal"))
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                else
                {
                    shader = Shader.Find("HDRP/Unlit");
                }

                if(shader != null)
                {
                    return shader;
                }
                else
                {
                    return getUsedRenderPipeline().defaultShader;
                }
            }
        }

        public static Shader GetDefaultParticleShader()
        {
            if (getUsedRenderPipeline() == null)
                return Shader.Find("Particles/Standard Unlit");
            else
                return getUsedRenderPipeline().defaultParticleMaterial.shader;
        }

        /// <summary>
        /// Returns the current pipline. Returns NULL if it's the standard render pipeline.
        /// </summary>
        /// <returns></returns>
        static UnityEngine.Rendering.RenderPipelineAsset getUsedRenderPipeline()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            else
                return UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        }

    }
}
#endif