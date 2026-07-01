using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Kamgam.SandGame
{
    public enum PixelColorStrategy
    {
        FromImage = 0,
        FromImageDarken20,
        FromImageDarken40,
        Color0,
        Color0Darken20,
        Color0Darken40,
    }

    [BurstCompile]
    [System.Serializable]
    public struct PixelMaterial
    {
        public PixelMaterialId id;

        /// <summary>
        /// The color in the source image.
        /// </summary>
        [UnityEngine.Serialization.FormerlySerializedAs("sourceColor")]
        public UnityEngine.Color32 colorInImage;

        // Behaviours
        [Header("Behaviours")]
        public PixelBehaviour behaviour0;
        public PixelBehaviour behaviour1;
        public PixelBehaviour behaviour2;
        public PixelBehaviour behaviour3;

        [Header("Properties")]

        // Density
        /// <summary>
        /// Density in Kg / m³, see: https://en.wikipedia.org/wiki/Density<br />
        /// Steam: 0.6<br />
        /// Air: 1.2<br />
        /// Wood: 700<br />
        /// Water: 1000<br />
        /// Sand: 1600<br />
        /// Granite: 2700<br />
        /// </summary>
        [Tooltip("Density in Kg per cubic meter")]
        public float density;

        /// <summary>
        /// A value between 0 and 1. A value of zero feels like ice, a value of 1 will make it come to rest very quickly.<br />
        /// See: https://docs.unity3d.com/Manual/class-PhysicMaterial.html
        /// </summary>
        [Tooltip("A value between 0 and 1. A value of zero feels like ice, a value of 1 will make it come to rest very quickly.")]
        [Range(0, 1)]
        public float friction;

        [Tooltip("Scale on the gravity force.")]
        [Range(0, 1)]
        public float gravityScale;

        [Tooltip("A value between 0 and 1 that defines how much of the velocity is preserved if the pixel hits something and is reflected.")]
        [Range(0,1)]
        public float bouncyness;

        // Movement
        /// <summary>
        /// The maximum distance (in pixels per frame!!!) a material can flow in one step. You could view this as the inverse of viscosity.<br />
        /// This must always be below or equal to PixelWorld.SimulationMargin.<br />
        /// NOTICE: It is in pixels per frame which is inconsistent with the other values that are in SI units.
        ///         I have not changed it because it is only used in the simulation phase which operates on pixels.
        /// </summary>
        [SerializeField]
        private int flowSpeed;
        public int GetFlowSpeed()
        {
            return Mathf.Min(flowSpeed, PixelWorld.SimulationMargin);
        }

        /// <summary>
        /// How much damage this pixel is dealing to others (damage per second).
        /// </summary>
        [Tooltip("How much damage this pixel is dealing to others (damage per second).")]
        public float damage;

        /// <summary>
        /// How much damage this pixel does to itself (damage per second).
        /// </summary>
        [Tooltip("How much damage this pixel does to itself (damage per second).")]
        public float selfDamage;

        /// <summary>
        /// How good the material is in receiving heat energy.<br />
        /// What ratio of heat it conducts from other pixels per second.<br />
        /// 1 = it will reach the source pixels heat in one second.
        /// </summary>
        [Tooltip("How good the material is in receiving heat energy.\n" +
            "What ratio of heat it conducts from other pixels per second.\n" +
            "1 = it will reach the source pixels heat in one second.")]
        public float heatConductivity;

        /// <summary>
        /// How fast the pixel will loose health while burning.
        /// </summary>
        [Tooltip("How fast the pixel will loose health while burning.")]
        public float heatSensitivity;

        /// <summary>
        /// Can this material burn if the tempreature if above the ignitionTemperature?
        /// </summary>
        [Tooltip("Can this material burn if the tempreature if above the ignitionTemperature?")]
        public byte flammable;

        /// <summary>
        /// What material does this convert to if the temperature reaches the range of the according aggregate state. Set to 'Empty' to ignore aggregate changes.
        /// </summary>
        [Header("Aggregate State Change")]
        [Tooltip("What material does this convert to if the temperature reaches the range of the according aggregate state. Set to 'Empty' to ignore aggregate changes.")]
        public PixelMaterialId solidStateMaterialId;

        /// <summary>
        /// Temperature at which the pixel will melt (in degrees CELCIUS).
        /// </summary>
        [Tooltip("Temperature at which the pixel will melt (in degrees CELCIUS).")]
        public float meltingTemperature;

        public PixelMaterialId liquidStateMaterialId;

        /// <summary>
        /// Temperature at which the pixel will change from liquid to gas (in degrees CELCIUS).
        /// </summary>
        [Tooltip("Temperature at which the pixel will change from liquid to gas (in degrees CELCIUS).")]
        public float boilingTemperature;

        public PixelMaterialId gasStateMaterialId;

        /// <summary>
        /// The temperature at which the pixel will ignite and start burning is it is flammable (in degrees CELCIUS).<br />
        /// See: https://en.wikipedia.org/wiki/Autoignition_temperature<br />
        /// Examples:
        ///  Water: 100 °C (well, it does never ignite, but it boils)
        ///  Newspapers: 170 °C
        ///  Plastic: 250 °C
        ///  Gasoline: 280 °C
        ///  Wood: 300 °C
        ///  Paper: 360 °C
        ///  Cotton: 450 °C
        ///  Tin: 940 °C
        ///  Iron: 1300 °C
        /// </summary>
        [Tooltip("The temperature at which the pixel will ignite and start burning is it is flammable (in degrees CELCIUS).")]
        public float ignitionTemperature;

        [Header("Start values")]

        public byte startsAwake;

        public float startTemperature;

        public float startHealth;


        // Colors
        [Header("Colorization")]
        public PixelColorStrategy colorStrategy;
        public float colorDarkenChanceRatio;
        public UnityEngine.Color32 color0;
        public UnityEngine.Color32 color1;
        public UnityEngine.Color32 color2;
        public UnityEngine.Color32 color3;

        public void ApplyPropertiesToPixel(ref Pixel pixel)
        {
            pixel.health = startHealth;
            pixel.sleepCounter = (byte)(startsAwake == 1 ? Pixel.SleepCounterResetValue : Pixel.SleepCounterIsSleepingValue);
            pixel.temperature = startTemperature;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixelColors(ref Pixel pixel, ref Unity.Mathematics.Random rnd)
        {
            switch (colorStrategy)
            {
                case PixelColorStrategy.FromImage:
                    break;

                case PixelColorStrategy.FromImageDarken20:
                    if (rnd.NextFloat() < colorDarkenChanceRatio)
                    {
                        pixel.r = (byte)math.clamp(pixel.r - 20, 0, 255);
                        pixel.g = (byte)math.clamp(pixel.g - 20, 0, 255);
                        pixel.b = (byte)math.clamp(pixel.b - 20, 0, 255);
                    }
                    break;

                case PixelColorStrategy.FromImageDarken40:
                    if (rnd.NextFloat() < colorDarkenChanceRatio)
                    {
                        pixel.r = (byte)math.clamp(pixel.r - 40, 0, 255);
                        pixel.g = (byte)math.clamp(pixel.g - 40, 0, 255);
                        pixel.b = (byte)math.clamp(pixel.b - 40, 0, 255);
                    }
                    break;

                case PixelColorStrategy.Color0:
                    pixel.r = color0.r;
                    pixel.g = color0.g;
                    pixel.b = color0.b;
                    pixel.a = color0.a;
                    break;

                case PixelColorStrategy.Color0Darken20:
                    if (rnd.NextFloat() < colorDarkenChanceRatio)
                    {
                        pixel.r = (byte)math.clamp(color0.r - 20, 0, 255);
                        pixel.g = (byte)math.clamp(color0.g - 20, 0, 255);
                        pixel.b = (byte)math.clamp(color0.b - 20, 0, 255);
                        pixel.a = color0.a;
                    }
                    else
                    {
                        pixel.r = color0.r;
                        pixel.g = color0.g;
                        pixel.b = color0.b;
                        pixel.a = color0.a;
                    }
                    break;

                case PixelColorStrategy.Color0Darken40:
                    if (rnd.NextFloat() < colorDarkenChanceRatio)
                    {
                        pixel.r = (byte)math.clamp(color0.r - 40, 0, 255);
                        pixel.g = (byte)math.clamp(color0.g - 40, 0, 255);
                        pixel.b = (byte)math.clamp(color0.b - 40, 0, 255);
                        pixel.a = color0.a;
                    }
                    else
                    {
                        pixel.r = color0.r;
                        pixel.g = color0.g;
                        pixel.b = color0.b;
                        pixel.a = color0.a;
                    }
                    break;

                default:
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBehaviour(PixelBehaviour behavour)
        {
            if (behaviour0 == PixelBehaviour.None)
                return false;

            return behaviour0 == behavour || behaviour1 == behavour || behaviour2 == behavour || behaviour3 == behavour;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMovable()
        {
            return HasBehaviour(PixelBehaviour.MoveLikeLiquid)
                || HasBehaviour(PixelBehaviour.MoveLikeSand)
                || HasBehaviour(PixelBehaviour.MoveLikeGas);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAffectedByGravity()
        {
            return HasBehaviour(PixelBehaviour.MoveLikeLiquid)
                || HasBehaviour(PixelBehaviour.MoveLikeSand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFlammable()
        {
            return flammable == 1;
        }
    }
}
