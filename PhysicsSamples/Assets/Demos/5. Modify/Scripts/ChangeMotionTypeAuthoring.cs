using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using Unity.Rendering;
using UnityEngine;

public struct ChangeMotionType : IComponentData
{
    public BodyMotionType NewMotionType;
    public PhysicsVelocity DynamicInitialVelocity;
    public float TimeLimit;
    public bool SetVelocityToZero;
    internal float Timer;
}

public struct ChangeMotionMaterials : ISharedComponentData, IEquatable<ChangeMotionMaterials>
{
    public UnityEngine.Material DynamicMaterial;
    public UnityEngine.Material KinematicMaterial;
    public UnityEngine.Material StaticMaterial;

    public bool Equals(ChangeMotionMaterials other) =>
        Equals(DynamicMaterial, other.DynamicMaterial)
        && Equals(KinematicMaterial, other.KinematicMaterial)
        && Equals(StaticMaterial, other.StaticMaterial);

    public override bool Equals(object obj) => obj is ChangeMotionMaterials other && Equals(other);

    public override int GetHashCode() =>
        unchecked((int)math.hash(new int3(
            DynamicMaterial != null ? DynamicMaterial.GetHashCode() : 0,
            KinematicMaterial != null ? KinematicMaterial.GetHashCode() : 0,
            StaticMaterial != null ? StaticMaterial.GetHashCode() : 0
        )));
}

public class ChangeMotionTypeAuthoring : MonoBehaviour
{
    public UnityEngine.Material DynamicMaterial;
    public UnityEngine.Material KinematicMaterial;
    public UnityEngine.Material StaticMaterial;

    [Range(0, 10)] public float TimeToSwap = 1.0f;
    public bool SetVelocityToZero = false;
}

class ChangeMotionTypeAuthoringBaker : Baker<ChangeMotionTypeAuthoring>
{
    public override void Bake(ChangeMotionTypeAuthoring authoring)
    {
        var velocity = new PhysicsVelocity();
        var physicsBodyAuthoring = GetComponent<PhysicsBodyAuthoring>();
        if (physicsBodyAuthoring != null)
        {
            velocity.Linear = physicsBodyAuthoring.InitialLinearVelocity;
            velocity.Angular = physicsBodyAuthoring.InitialAngularVelocity;
        }

        AddComponent(new ChangeMotionType
        {
            NewMotionType = BodyMotionType.Dynamic,
            DynamicInitialVelocity = velocity,
            TimeLimit = authoring.TimeToSwap,
            Timer = authoring.TimeToSwap,
            SetVelocityToZero = authoring.SetVelocityToZero
        });

        AddSharedComponentManaged(new ChangeMotionMaterials
        {
            DynamicMaterial = authoring.DynamicMaterial,
            KinematicMaterial = authoring.KinematicMaterial,
            StaticMaterial = authoring.StaticMaterial
        });
        AddComponent<PhysicsMassOverride>();
    }
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial class ChangeMotionTypeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        using (var commandBuffer = new EntityCommandBuffer(Allocator.TempJob))
        {
            Entities
                .WithName("ChangeMotionTypeJob")
                .WithoutBurst()
                .ForEach((Entity entity, ref ChangeMotionType modifier, in ChangeMotionMaterials materials, in RenderMesh renderMesh) =>
                {
                    // tick timer
                    modifier.Timer -= deltaTime;

                    if (modifier.Timer > 0f)
                        return;

                    // reset timer
                    modifier.Timer = modifier.TimeLimit;

                    var setVelocityToZero = (byte)(modifier.SetVelocityToZero ? 1 : 0);
                    // make modifications based on new motion type
                    UnityEngine.Material material = renderMesh.material;
                    switch (modifier.NewMotionType)
                    {
                        case BodyMotionType.Dynamic:
                            // a dynamic body has PhysicsVelocity and PhysicsMassOverride is disabled if it exists
                            if (!HasComponent<PhysicsVelocity>(entity))
                                commandBuffer.AddComponent(entity, modifier.DynamicInitialVelocity);
                            if (HasComponent<PhysicsMassOverride>(entity))
                                commandBuffer.SetComponent(entity, new PhysicsMassOverride { IsKinematic = 0, SetVelocityToZero = setVelocityToZero });

                            material = materials.DynamicMaterial;
                            break;
                        case BodyMotionType.Kinematic:
                            // a static body has PhysicsVelocity and PhysicsMassOverride is enabled if it exists
                            // note that a 'kinematic' body is really just a dynamic body with infinite mass properties
                            // hence you can create a persistently kinematic body by setting properties via PhysicsMass.CreateKinematic()
                            if (!HasComponent<PhysicsVelocity>(entity))
                                commandBuffer.AddComponent(entity, modifier.DynamicInitialVelocity);
                            if (HasComponent<PhysicsMassOverride>(entity))
                                commandBuffer.SetComponent(entity, new PhysicsMassOverride { IsKinematic = 1, SetVelocityToZero = setVelocityToZero });

                            material = materials.KinematicMaterial;
                            break;
                        case BodyMotionType.Static:
                            // a static body is one with a PhysicsCollider but no PhysicsVelocity
                            if (HasComponent<PhysicsVelocity>(entity))
                                commandBuffer.RemoveComponent<PhysicsVelocity>(entity);

                            material = materials.StaticMaterial;
                            break;
                    }

                    // assign the new render mesh material
                    var newRenderMesh = renderMesh;
                    newRenderMesh.material = material;
                    commandBuffer.SetSharedComponentManaged(entity, newRenderMesh);

                    // move to next motion type
                    modifier.NewMotionType = (BodyMotionType)(((int)modifier.NewMotionType + 1) % 3);
                }).Run();

            commandBuffer.Playback(EntityManager);
        }
    }
}
