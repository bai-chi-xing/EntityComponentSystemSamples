using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

public struct PhysicsPyramid : IComponentData {}

public struct CreatePyramids : IComponentData
{
    public Entity BoxEntity;
    public int Count;
    public int Height;
    public int Space;
    public float3 StartPosition;
    public float3 BoxSize;
}


public class CreatePyramidsBehaviour : MonoBehaviour
{
    public GameObject boxPrefab;
    public int Count = 5;
    public int Height = 5;
    public int Space = 2;

    class CreatePyramidsBaker : Baker<CreatePyramidsBehaviour>
    {
        public override void Bake(CreatePyramidsBehaviour authoring)
        {
            var sourceEntity = GetEntity(authoring.boxPrefab);
            if (sourceEntity == Entity.Null)
                return;

            var boxSize = float3.zero;
            var renderer = authoring.boxPrefab.GetComponent<Renderer>();
            if (renderer != null)
            {
                boxSize = renderer.bounds.size;
            }

            var createPyramids = new CreatePyramids
            {
                BoxEntity = sourceEntity,
                Count = authoring.Count,
                Height = authoring.Height,
                Space = authoring.Space,
                StartPosition = authoring.transform.position,
                BoxSize = boxSize
            };
            AddComponent(createPyramids);
        }
    }
}


[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class CreatePyramidsSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<CreatePyramids>();
    }

    protected override void OnUpdate()
    {
        Entities
            .WithoutBurst()
            .WithStructuralChanges()
            .ForEach((Entity creatorEntity, in CreatePyramids creator) =>
            {
                float3 boxSize = creator.BoxSize;
                int boxCount = creator.Count * (creator.Height * (creator.Height + 1) / 2);

                var positions = new NativeArray<float3>(boxCount, Allocator.Temp);

                int boxIdx = 0;
                for (int p = 0; p < creator.Count; p++)
                {
                    for (int i = 0; i < creator.Height; i++)
                    {
                        int rowSize = creator.Height - i;
                        float3 start = new float3(-rowSize * boxSize.x * 0.5f + boxSize.x * 0.5f, i * boxSize.y, 0);
                        for (int j = 0; j < rowSize; j++)
                        {
                            float3 shift = new float3(j * boxSize.x, 0f, p * boxSize.z * creator.Space);
                            positions[boxIdx] = creator.StartPosition;
                            positions[boxIdx] += start + shift;
                            boxIdx++;
                        }
                    }
                }

                var pyramidComponent = new PhysicsPyramid();
                for (int i = 0; i < positions.Length; i++)
                {
                    var entity = EntityManager.Instantiate(creator.BoxEntity);
                    EntityManager.AddComponentData(entity, pyramidComponent);
                    EntityManager.SetComponentData(entity, new Translation() { Value = positions[i] });
                }

                EntityManager.DestroyEntity(creatorEntity);
            }).Run();
    }
}
