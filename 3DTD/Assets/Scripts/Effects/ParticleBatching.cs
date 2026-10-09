using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Setup shared by the batched backends (BatchedEffect for one-shots, FlightBatch for flights): which particle
// systems they can reproduce, how the systems of their shared copy are configured, and the particle buffers.
public static class ParticleBatching
{
    // Sub-emitters and velocity or forces in local space can't be reproduced by emitting into a shared copy
    public static bool CanBatch(ParticleSystem system)
    {
        if (system.subEmitters.enabled && system.subEmitters.subEmittersCount > 0)
            return false;
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        if (velocity.enabled && velocity.space == ParticleSystemSimulationSpace.Local)
            return false;
        ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
        if (force.enabled && force.space == ParticleSystemSimulationSpace.Local)
            return false;
        return true;
    }

    // Mesh and locally aligned particles turn with their play's rotation; velocity-aligned ones follow their
    // (rotated) velocity on their own
    public static bool NeedsRotate3D(ParticleSystem system)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        bool mesh = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh;
        bool localAligned = renderer != null && renderer.alignment == ParticleSystemRenderSpace.Local;
        return (mesh || localAligned) && (renderer == null || renderer.alignment != ParticleSystemRenderSpace.Velocity);
    }

    // The shared copy simulates in world space at the origin, never plays or emits on its own (the backend emits
    // into it), casts no shadows and keeps its particle lights within BatchedEffect.MaxLightsPerSystem. Read the
    // emission and inherit velocity settings before calling this: it switches both modules off.
    public static void ConfigureShared(ParticleSystem system, bool rotate3D, int minParticles)
    {
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = false;
        main.loop = true;
        main.prewarm = false;
        main.startDelay = 0f;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = Mathf.Max(main.maxParticles, minParticles);
        if (rotate3D && !main.startRotation3D)
        {
            ParticleSystem.MinMaxCurve roll = main.startRotation;
            main.startRotation3D = true;
            main.startRotationZ = roll;
        }

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.InheritVelocityModule inherit = system.inheritVelocity;
        inherit.enabled = false;

        ParticleSystem.LightsModule lights = system.lights;
        if (lights.enabled)
            lights.maxLights = Mathf.Min(lights.maxLights * 4, BatchedEffect.MaxLightsPerSystem);

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Grows a particle buffer to at least size (a power of two, so a busy frame doesn't regrow it every time)
    public static void EnsureBuffer(ref NativeArray<ParticleSystem.Particle> buffer, int size)
    {
        if (buffer.IsCreated && buffer.Length >= size)
            return;
        if (buffer.IsCreated)
            buffer.Dispose();
        buffer = new NativeArray<ParticleSystem.Particle>(Mathf.NextPowerOfTwo(size), Allocator.Persistent);
    }
}
