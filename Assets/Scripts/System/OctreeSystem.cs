using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

public class OctreeSystem : JobComponentSystem
{
    [Inject] SwarmData _swarmData;

    PrevCell _prev;
    struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;
        [ReadOnly] public ComponentDataArray<Position> position;
    }

    struct PrevCell
    {
        public NativeArray<float3> positions;
    }

    struct OctreeComponent
    {
        public Octree octree;
    }

    struct UpdateOctreeJob : IJob
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;
        [ReadOnly] public ComponentDataArray<Position> position;

        NativeMultiHashMap<int, NativeArray<int>> octreeMap;
        NativeArray<int> attached;

        [ReadOnly] private readonly int MAX_DEPTH;
        [ReadOnly] public int MAX_ENTITES;

        public int LookUpOctant(int code)
        {
            NativeArray<int> octant;
            NativeMultiHashMapIterator<int> it;
            octreeMap.TryGetFirstValue(code, out octant, out it);
        }

        public void Insert(int index, float3 position)
        {
            int i = 0;
            int currCount = 0;
            int locCode = 1;
            NativeArray<int> octantEnts;
            while (i++ < MAX_DEPTH && currCount <= MAX_ENTITES)
            {

            }
        }


        public void Execute()
        {
            for(int i = 0; i < Length; i++)
            {
                if(attached[i] != -1)
                {
                    Insert(i, position[i].Value);
                }
            }
        }

    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        foreach(OctreeComponent octree in GetEntities<OctreeComponent>())
        {
            return new UpdateOctreeJob
            {
                velocity = _swarmData.velocity,
                position = _swarmData.position

            }.Schedule(inputDeps);
        }

        return base.OnUpdate(inputDeps);
    }
}
