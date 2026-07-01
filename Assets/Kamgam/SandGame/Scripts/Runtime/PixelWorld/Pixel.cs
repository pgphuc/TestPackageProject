using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Kamgam.SandGame
{
    /// <summary>
    /// By default a pixel is an immovable, indestructible object with a position and a color.<br />
    /// Any additional properties of a pixel are added via components.
    /// 
    /// TODOs:
    /// * Move static properties like ignitinTemperature out of pixels and into materials (reduces memory consumption).
    /// </summary>
    [BurstCompile]
    [System.Serializable]
    public struct Pixel
    {
        public const int SleepCounterIsSleepingValue = 30;
        public const int SleepCounterResetValue = 0;

        // Material referenced by index
        public int materialIndex;

        /// <summary>
        /// Whether or not the pixel is empty.
        /// </summary>
        public byte hasValue; // Sadly IL2CPP does not like bool, thus we use byte.
                              // See: https://forum.unity.com/threads/argument-exception-on-windows-il2cpp-build-with-burst-jobs.836059/

        /// <summary>
        /// Whether or not the pixel has been loaded. Non loaded pixels are not simulated.
        /// </summary>
        public byte isLoaded;

        /// <summary>
        /// Used to distinguis between pixels that are free falling (aka they are accelerating due to gravity) or pixels that are not
        /// free falling (aka they have a connection to the ground, no gravity is applied to them).
        /// </summary>
        public byte isFreeFalling;

        /// <summary>
        /// If a pixel is sliding then it has a higher likelyhood of sliding down diagonally. This is used to simulate different levels
        /// of sliding (sand slides perfectly for exmaple).
        /// </summary>
        public byte isSliding;

        /// <summary>
        /// If >= SleepCounterIsSleepingValue then the pixel is considered asleep and is not simulated.<br />
        /// Reset to 0 to wake the pixel up.
        /// </summary>
        public byte sleepCounter;

        /// <summary>
        /// Whether or not the pixel should be simulated in this frame.<br />
        /// Is set to false if a pixel has been simulated in this frame already or never needed to be simulated.<br />
        /// Used to avoid simulating it twice in one frame.
        /// </summary>
        public byte simulationScheduled;

        // Position
        /// <summary>
        /// Position in pixels.
        /// </summary>
        public int x;
        public int y;

        /// <summary>
        /// The positional delta in PIXELS based on the float velocity.
        /// If this becomes > 1 then it will be applied to the position.
        /// </summary>
        public float deltaX;
        public float deltaY;

        // Velocity
        /// <summary>
        /// Velocity in the x direction. Meassured in meters per second.
        /// </summary>
        public float velocityX;

        /// <summary>
        /// Velocity in the y direction.  Meassured in meters per second.
        /// </summary>
        public float velocityY;

        /// <summary>
        /// If 0 or below then the pixel might be destroyed.<br />
        /// Default health is 100.
        /// </summary>
        public float health;

        /// <summary>
        /// The current temperatue of a pixel in degrees CELCIUS. SI units, ya know ;)
        /// </summary>
        public float temperature;

        /// <summary>
        /// The id that links this pixel to a dynamic object in the unity scene.
        /// </summary>
        public int dynamicObjectId;

        /// <summary>
        /// UVs are the relative positions to the dynamic object.
        /// </summary>
        public float u;

        /// <summary>
        /// UVs are the relative positions to the dynamic object.
        /// </summary>
        public float v;

        // Color
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public void ApplyVelocityToPositionDelta(float deltaTime, int pixelsPerUnit)
        {
            deltaX += velocityX * deltaTime * pixelsPerUnit;
            deltaY += velocityY * deltaTime * pixelsPerUnit;
        }

        public void ApplyFriction(float deltaTime, ref Pixel ground, ref NativeArray<PixelMaterial> materials)
        {
            if (math.abs(velocityX) > 0.001f)
            {
                var material = GetMaterial(ref materials);
                var groundMaterial = ground.GetMaterial(ref materials);

                // v(t) = v0 - friction * g * t
                float friction = material.friction * groundMaterial.friction;
                if (velocityX > 0)
                    velocityX = math.max(0, velocityX - friction * 9.81f * deltaTime);
                else
                    velocityX = math.min(0, velocityX + friction * 9.81f * deltaTime);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StopMoving()
        {
            velocityX = 0f;
            deltaX = 0f;
            velocityY = 0f;
            deltaY = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSqrSpeed()
        {
            return velocityX * velocityX + velocityY * velocityY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasVelocity()
        {
            return math.abs(velocityX) > 0.001f || math.abs(velocityY) > 0.001f;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovementDeltaReachedPixels()
        {
            return math.abs(deltaX) >= 1f || math.abs(deltaY) >= 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLoaded() => isLoaded == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetVelocityXInPixels(int PixelsPerUnit)
        {
            return velocityX * PixelsPerUnit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetVelocityYInPixels(int PixelsPerUnit)
        {
            return velocityY * PixelsPerUnit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty() => hasValue == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFreeFalling() => isFreeFalling == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StopFreeFalling() => isFreeFalling = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartFreeFalling() => isFreeFalling = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAwake() => sleepCounter < SleepCounterIsSleepingValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAsleep() => sleepCounter >= SleepCounterIsSleepingValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RequiresSimulation() => simulationScheduled == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ScheduleSimulation() => simulationScheduled = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkAsSimulated() => simulationScheduled = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBurning(ref NativeArray<PixelMaterial> materials)
        {
            return temperature >= materials[materialIndex].ignitionTemperature && materials[materialIndex].IsFlammable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PixelMaterialId Burn(float deltaTime, ref NativeArray<PixelMaterial> materials)
        {
            // Increase temperature.
            PixelMaterialId newAggregateId = ChangeTemperatureBy(temperature * 2f * deltaTime, ref materials);

            // Reduce health
            float deltaToStartTemperature = math.abs(materials[materialIndex].startTemperature - temperature);
            ChangeHealthBy(-deltaToStartTemperature * deltaTime * materials[materialIndex].heatSensitivity);

            return newAggregateId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetHeatConductivity(float deltaTime, ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].heatConductivity * deltaTime;
        }

        /// <summary>
        /// The limit ensures the pixel never travel farther than the allowed max distance.<br />
        /// This impacts simulation accuracy at high velocities but it also encure we never leave the thread pixel buffer boundaries.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDeltaXInt(int limit = PixelWorld.SimulationMargin) => math.clamp((int)deltaX, -(limit - 1), limit - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDeltaYInt(int limit = PixelWorld.SimulationMargin) => math.clamp((int)deltaY, -(limit - 1), limit - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBehaviour(PixelBehaviour behavour, ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].HasBehaviour(behavour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMovableOrEmpty(ref NativeArray<PixelMaterial> materials)
        {
            return IsEmpty() || materials[materialIndex].IsMovable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMovable(ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].IsMovable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAffectedByGravity(ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].IsMovable() && materials[materialIndex].gravityScale > 0.001f && !HasBehaviour(PixelBehaviour.MoveLikeGas, ref materials);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sleep()
        {
            sleepCounter = SleepCounterIsSleepingValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WakeUp()
        {
            sleepCounter = SleepCounterResetValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVelocityX(float value)
        {
            if (value == 0f)
            {
                SetVelocityXToPseudoZero();
            }
            else
            {
                velocityX = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVelocityDirectionX(float value)
        {
            if (math.abs(value) > 0.0001f)
            {
                velocityX = value < 0 ? -0.0001f : 0.0001f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVelocityXToPseudoZero()
        {
            if (math.abs(velocityX) > 0.0001f)
            {
                velocityX = velocityX < 0 ? -0.0001f : 0.0001f;
            }
            else
            {
                velocityX = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVelocityY(float value)
        {
            if (value == 0f)
            {
                SetVelocityYToPseudoZero();
            }
            else
            {
                velocityY = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVelocityDirectionY(float value)
        {
            if (math.abs(value) >= 0.0001f)
            {
                velocityY = value < 0 ? -0.0001f : 0.0001f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVelocityYToPseudoZero()
        {
            if (math.abs(velocityY) >= 0.0001f)
            {
                velocityY = velocityY < 0 ? -0.0001f : 0.0001f;
            }
            else
            {
                velocityY = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColorRed() => SetColor(255, 0, 0, 255);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColorGreen() => SetColor(0, 255, 0, 255);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColorBlue() => SetColor(0, 0, 255, 255);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColorBlack() => SetColor(0, 0, 0, 255);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColor(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetColor(byte r, byte g, byte b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ChangeHealthBy(float hp)
        {
            health = math.clamp(health + hp, 0f, float.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PixelMaterialId ChangeTemperatureBy(float temperatureDelta, ref NativeArray<PixelMaterial> materials)
        {
            PixelMaterial material = materials[materialIndex];

            // Record initial aggregate state (0 = solid, 1 = liquid, 2 = gas). We ignore plasma states.
            float oldTemperature = temperature;
            int oldAggregateState = oldTemperature <= material.meltingTemperature ? 0 : (oldTemperature >= material.boilingTemperature ? 2 : 1);

            // Change temperature
            temperature = math.clamp(temperature + temperatureDelta, -273f, float.MaxValue);

            // Check if the aggregate state should change. If yes then return the new material id.
            int newAggregateState = temperature <= material.meltingTemperature ? 0 : (temperature >= material.boilingTemperature ? 2 : 1);
            if (oldAggregateState != newAggregateState)
            {
                PixelMaterialId newMaterialId;
                if (newAggregateState == 0)
                {
                    newMaterialId = material.solidStateMaterialId;
                }
                else if (newAggregateState == 1)
                {
                    newMaterialId = material.liquidStateMaterialId;
                }
                else
                {
                    newMaterialId = material.gasStateMaterialId;
                }

                // If the new material id is the empty id then pretend no change was needed and return the current id.
                if (newMaterialId == PixelMaterialId.Empty)
                    return material.id;
                else
                    return newMaterialId;
            }
            else
            {
                return material.id;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PixelMaterial GetMaterial(ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDead()
        {
            return health <= 0.001f;
        }

        /// <summary>
        /// Calculate the damage that should be applied based on delta time.
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetDamage(float deltaTime, ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].damage * deltaTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSelfDamage(float deltaTime, ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].selfDamage * deltaTime;
        }

        /// <summary>
        /// Can this pixel be combined with other non-empty pixels?
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCombineSource(ref NativeArray<PixelMaterial> materials)
        {
            return !IsEmpty() && HasBehaviour(PixelBehaviour.CombineByCorrosion, ref materials);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidCombineTarget(ref NativeArray<PixelMaterial> materials)
        {
            return !HasBehaviour(PixelBehaviour.Static, ref materials);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasSameMaterial(ref Pixel pixel)
        {
            return materialIndex == pixel.materialIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCombinableWith(ref Pixel pixel, ref NativeArray<PixelMaterial> materials)
        {
            // Different types or self-combinable
            return IsCombineSource(ref materials) 
                && pixel.IsValidCombineTarget(ref materials)
                && (!HasSameMaterial(ref pixel));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDenserThanAndCanBeSwappedWith(ref Pixel pixel, ref NativeArray<PixelMaterial> materials)
        {
            return IsAffectedByGravity(ref materials)
                && pixel.IsMovableOrEmpty(ref materials)
                && pixel.GetDensity(ref materials) < GetDensity(ref materials);
        }

        public float GetDensity(ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].density;
        }

        public int GetFlowSpeed(ref NativeArray<PixelMaterial> materials)
        {
            return materials[materialIndex].GetFlowSpeed();
        }
    }
}