using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Step3
{
    public class FindNearest : MonoBehaviour
    {
        public NativeArray<float3> TargetPositions;
        public NativeArray<float3> SeekerPositions;
        public NativeArray<float3> NearestTargetPositions;

        public void Awake()
        {
            Spawner spawner = Object.FindObjectOfType<Spawner>();
            TargetPositions = new NativeArray<float3>(spawner.NumTargets, Allocator.Persistent);
            SeekerPositions = new NativeArray<float3>(spawner.NumSeekers, Allocator.Persistent);
            NearestTargetPositions = new NativeArray<float3>(spawner.NumSeekers, Allocator.Persistent);
        }

        public void OnDestroy()
        {
            TargetPositions.Dispose();
            SeekerPositions.Dispose();
            NearestTargetPositions.Dispose();
        }

        public void Update()
        {
            for (int i = 0; i < TargetPositions.Length; i++)
            {
                TargetPositions[i] = Spawner.TargetTransforms[i].localPosition;
            }

            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                SeekerPositions[i] = Spawner.SeekerTransforms[i].localPosition;
            }

            FindNearestJob findJob = new FindNearestJob
            {
                TargetPositions = TargetPositions,
                SeekerPositions = SeekerPositions,
                NearestTargetPositions = NearestTargetPositions,
            };

            // Execute will be called once for every element of the SeekerPositions array,
            // with every index from 0 up to (but not including) the length of the array.
            // The Execute calls will be split into batches of 64.
            JobHandle findHandle = findJob.Schedule(SeekerPositions.Length, 64);
            
            findHandle.Complete();

            for (int i = 0; i < SeekerPositions.Length; i++)
            {
                Debug.DrawLine(SeekerPositions[i], NearestTargetPositions[i]);
            }
        }
    }
}