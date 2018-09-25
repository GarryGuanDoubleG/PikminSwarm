using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;

public class OctreeSystem : JobComponentSystem
{    

    struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<SwarmModelFormation> swarm;

        public ComponentDataArray<Position> position;
    }

    struct PrevCell
    {
        public NativeArray<float3> positions;
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {



        return base.OnUpdate(inputDeps);
    }
}
