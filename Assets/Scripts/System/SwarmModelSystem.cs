using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

public class SwarmModelSystem : JobComponentSystem
{
    [Inject] SwarmData _swarmData;

    NativeArray<int> _targetIndices;
	struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<SwarmModelFormation> swarm;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;
    }

    struct TargetData
    {
        public ColliderGrid grid;
        public TargetComponent target;
    }

    struct SwarmMoveJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public NativeArray<int> indices;
        [ReadOnly] public Vector3 rootPosition;
        [ReadOnly] public List<Vector3> nodes;
        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;
        [ReadOnly] public float deltaT;

        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

        public void Execute(int i)
        {
            int targetIndex = indices[i];
            float3 targetPos = nodes[targetIndex] + rootPosition;
            float3 newVel = math.lerp(velocity[i].Value, targetPos - position[i].Value, math.exp(deltaT));
            float lengthSQ = math.lengthsq(newVel);
            if (lengthSQ < minVel * minVel)
                newVel = minVel * math.normalize(newVel);
            else if (lengthSQ > maxVel * maxVel)
                newVel = maxVel * math.normalize(newVel);

            if (math.distance(targetPos, position[i].Value) < 1.0f)
                newVel = float3.zero;

            velocity[i] = new Velocity { Value = newVel };
        }
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        _targetIndices.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if(_targetIndices.IsCreated)
            _targetIndices.Dispose();
        _targetIndices = new NativeArray<int>(_swarmData.Length, Allocator.TempJob);

        //TODO work on multiple targets
        foreach(var entity in GetEntities<GameObject>())
        {
            var grid = entity.grid;
            int targetNodeCount = grid.PointList.Count;
            int offset = targetNodeCount / _swarmData.Length;
            int count = 0;            

            for (int i = 0; i < targetNodeCount; i += offset)
                _targetIndices[count++] = i;

            return new SwarmMoveJob
            {
                Length = _swarmData.Length,
                position = _swarmData.position,
                rotation = _swarmData.rotation,
                velocity = _swarmData.velocity,
                indices = _targetIndices,
                nodes = grid.PointList,
                rootPosition = grid.gameObject.transform.position,
                minVel = Bootstrap.settings._minVel,
                maxVel = Bootstrap.settings._maxVel,
            }.Schedule(_swarmData.Length, 32, inputDeps);
        }
        return inputDeps;
    }
}
