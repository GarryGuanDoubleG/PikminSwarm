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
    NativeArray<int> _pointBoneIndices; //bone index of swarm point 
    NativeArray<float3> _targetBoneOffests;
    NativeArray<float3> _bonePositions;
    NativeArray<Quaternion> _boneRotations;
    NativeArray<Quaternion> _boneInverseBaseRotations;
    

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
          
        [ReadOnly] public float3 rootPosition;
        [ReadOnly] public float3 rootScale;
        [ReadOnly] public Quaternion rootRotation;

        [ReadOnly] public NativeArray<int> swarmIndex;
        [ReadOnly] public NativeArray<int> boneIndices;
        [ReadOnly] public NativeArray<float3> boneOffsets;
        [ReadOnly] public NativeArray<float3> bonePositions;
        [ReadOnly] public NativeArray<Quaternion> boneRotations;

        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;
        [ReadOnly] public float deltaT;

        [ReadOnly] public ComponentDataArray<Position> position;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

        public void Execute(int i)
        {
            int targetIndex = swarmIndex[i];
            int boneIndex = boneIndices[targetIndex];
            float3 bonePos = bonePositions[boneIndex];
            Quaternion boneRot = boneRotations[boneIndex];

            float3 boneOffsetVec = boneOffsets[targetIndex];
            float3 targetPos = rootRotation * boneOffsetVec;
            targetPos = (float3)(boneRot * targetPos);
            targetPos += bonePos;
            //targetPos = boneRot * targetPos;

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

        if (_targetBoneOffests.IsCreated)
            _targetBoneOffests.Dispose();

        if (_pointBoneIndices.IsCreated)
            _pointBoneIndices.Dispose();

        if(_bonePositions.IsCreated)
            _bonePositions.Dispose();

        if (_boneRotations.IsCreated)
            _boneRotations.Dispose();

        if (_boneInverseBaseRotations.IsCreated)
            _boneInverseBaseRotations.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (_targetIndices.IsCreated)
        {
            _targetIndices.Dispose();
            _boneRotations.Dispose();
            _bonePositions.Dispose();
        }

        _targetIndices = new NativeArray<int>(_swarmData.Length, Allocator.TempJob);

        //TODO work on multiple targets
        foreach(var entity in GetEntities<GridPoints>())
        {
            var grid = entity.grid;
            if(grid != _currentGrid)
            {
                _currentGrid = grid;
                if (_targetBoneOffests.IsCreated)
                {
                    _targetBoneOffests.Dispose();
                    _pointBoneIndices.Dispose();
                }

                _targetBoneOffests = new NativeArray<float3>(grid.BonePointList.Count, Allocator.Persistent);
                _pointBoneIndices = new NativeArray<int>(grid.BoneIndexList.Count, Allocator.Persistent);
                _boneInverseBaseRotations = new NativeArray<Quaternion>(grid.BoneList.Count, Allocator.Persistent);
                for(int i = 0; i < grid.BonePointList.Count; i++)
                {
                    _targetBoneOffests[i] = grid.BonePointList[i];
                    _pointBoneIndices[i] = grid.BoneIndexList[i];                    
                }

                for(int i = 0; i < grid.BoneList.Count; i++)
                {
                    _boneInverseBaseRotations[i] = Quaternion.Inverse(grid.BoneList[i].rotation);
                }
            }

            int targetNodeCount = _targetBoneOffests.Length;
            int offset = (int)math.ceil((float)targetNodeCount / (float)_swarmData.Length);
            int count = 0;

            for (int i = 0; i < targetNodeCount; i += offset)
            {
                if (count >= _swarmData.Length)
                    break;

                _targetIndices[count++] = i;
            }

            _bonePositions = new NativeArray<float3>(grid.BoneList.Count, Allocator.TempJob);
            _boneRotations = new NativeArray<Quaternion>(grid.BoneList.Count, Allocator.TempJob);
            for (int i = 0; i < grid.BoneList.Count; i++)
            {
                var transform = grid.BoneList[i];
                
                _bonePositions[i] = transform.position;
                _boneRotations[i] = transform.rotation * _boneInverseBaseRotations[i];
                //_boneRotations[i] = transform.rotation;
            }

            return new SwarmMoveJob
            {
                Length = _swarmData.Length,
                position = _swarmData.position,
                rotation = _swarmData.rotation,                
                velocity = _swarmData.velocity,
                swarmIndex = _targetIndices,
                boneOffsets = _targetBoneOffests,
                boneIndices = _pointBoneIndices,
                bonePositions = _bonePositions,
                boneRotations = _boneRotations,
                rootPosition = grid.rootArmature.position,
                rootRotation = grid.rootArmature.rotation,
                rootScale = grid.gameObject.transform.localScale,
                minVel = Bootstrap.settings._minVel,
                maxVel = Bootstrap.settings._maxVel,
            }.Schedule(_swarmData.Length, 32, inputDeps);
        }


        return inputDeps;
    }
}
