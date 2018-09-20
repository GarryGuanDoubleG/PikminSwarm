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
    NativeArray<float3> _targetPoints;
    ColliderGrid _currentGrid;

	struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<SwarmModelFormation> swarm;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;
    }

    struct GridPoints
    {
        public ColliderGrid grid;
    }

    struct SwarmMoveJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public NativeArray<int> indices;
        [ReadOnly] public NativeArray<float3> meshPoints;
        [ReadOnly] public float3 rootPosition;        
        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;
        [ReadOnly] public float deltaT;

        [ReadOnly] public float3 rootScale;
        [ReadOnly] public Quaternion rootRotation;

        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

        public void Execute(int i)
        {
            int targetIndex = indices[i];
            float3 targetPos = meshPoints[targetIndex];
            targetPos = targetPos * rootScale;
            targetPos = rootRotation * targetPos;            
            targetPos += rootPosition;
            float3 newVel = math.lerp(velocity[i].Value, targetPos - position[i].Value, math.exp(deltaT));
            float lengthSQ = math.lengthsq(newVel);
            if (lengthSQ < minVel * minVel)
                newVel = minVel * math.normalize(newVel);
            else if (lengthSQ > maxVel * maxVel)
                newVel = maxVel * math.normalize(newVel);

            if (math.distance(targetPos, position[i].Value) <= .20f)
                newVel = float3.zero;

            velocity[i] = new Velocity { Value = newVel };
        }
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        if(_targetIndices.IsCreated)
            _targetIndices.Dispose();

        if (_targetPoints.IsCreated)
            _targetPoints.Dispose();

    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (_targetIndices.IsCreated)
        {
            _targetIndices.Dispose();
        }
        _targetIndices = new NativeArray<int>(_swarmData.Length, Allocator.TempJob);

        //TODO work on multiple targets
        foreach(var entity in GetEntities<GridPoints>())
        {
            var grid = entity.grid;
            if(grid != _currentGrid)
            {
                _currentGrid = grid;
                if (_targetPoints.IsCreated)
                    _targetPoints.Dispose();
                _targetPoints = new NativeArray<float3>(grid.PointList.Count, Allocator.Persistent);
                int i = 0;
                foreach(Vector3 point in grid.PointList)
                {
                    _targetPoints[i++] = point;
                }
            }

            int targetNodeCount = _targetPoints.Length;
            int offset = (int)math.ceil((float)targetNodeCount / (float)_swarmData.Length);
            int count = 0;

            for (int i = 0; i < targetNodeCount; i += offset)
            {
                if (count >= _swarmData.Length)
                    break;

                _targetIndices[count++] = i;
            }
            return new SwarmMoveJob
            {
                Length = _swarmData.Length,
                position = _swarmData.position,
                rotation = _swarmData.rotation,
                velocity = _swarmData.velocity,
                indices = _targetIndices,
                meshPoints = _targetPoints,
                rootPosition = grid.gameObject.transform.position,
                rootRotation = grid.gameObject.transform.rotation,
                rootScale = grid.gameObject.transform.localScale,
                minVel = Bootstrap.settings._minVel,
                maxVel = Bootstrap.settings._maxVel,
            }.Schedule(_swarmData.Length, 32, inputDeps);
        }
        return inputDeps;
    }
}
