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
    NativeArray<float3> _bonePositions;    
    NativeArray<BoneWeight> _boneWeights;
    NativeArray<Quaternion> _boneRotations;
    NativeArray<Quaternion> _boneInverseBaseRotations;
        
    ColliderGrid _currentGrid;

	struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<SwarmModelFormation> swarm;

        public ComponentDataArray<Position> position;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;
    }

    struct GridPoints
    {
        public ColliderGrid grid;
    }

    //struct BoneWeight
    //{
    //    public float3 position;
    //    public int4 boneIndices;
    //    public float4 boneWeights;        
    //}

    struct SwarmModelJob : IJobParallelFor
    {
        [ReadOnly] public int Length;

        [ReadOnly] public float3 rootPosition;
        [ReadOnly] public float3 rootScale;
        [ReadOnly] public Quaternion rootRotation;

        [ReadOnly] public NativeArray<float3> targetPositions;
        [ReadOnly] public NativeArray<int> targetIndices;        

        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;
        [ReadOnly] public float deltaT;

        [ReadOnly] public ComponentDataArray<Position> position;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

        public void Execute(int i)
        {
            int targetIndex = targetIndices[i];
            float3 targetPos = targetPositions[i];
            targetPos = rootRotation * targetPos;
            targetPos = math.mul(targetPos, rootScale);
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

    struct SwarmSkinnedModelJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
          
        [ReadOnly] public float3 rootPosition;
        [ReadOnly] public float3 rootScale;
        [ReadOnly] public Quaternion rootRotation;

        [ReadOnly] public NativeArray<int> targetIndices;
        [ReadOnly] public NativeArray<float3> cellPosition;
        [ReadOnly] public NativeArray<BoneWeight> boneGridCells;
        [ReadOnly] public NativeArray<float3> bonePositions;
        [ReadOnly] public NativeArray<Quaternion> boneRotations;

        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;
        [ReadOnly] public float deltaT;

        public ComponentDataArray<Position> position;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

        public void Execute(int index)
        {
            int targetIndex = targetIndices[index];
            BoneWeight boneCell = boneGridCells[targetIndex];

            float3 gridPoint = cellPosition[targetIndex];
            float3 targetPos = float3.zero;
            float3 weighedPoint = float3.zero;
            float3 weighedOffset = float3.zero;

            weighedPoint += boneCell.weight0 * bonePositions[boneCell.boneIndex0];
            weighedPoint += boneCell.weight1 * bonePositions[boneCell.boneIndex1];
            weighedPoint += boneCell.weight2 * bonePositions[boneCell.boneIndex2];
            weighedPoint += boneCell.weight3 * bonePositions[boneCell.boneIndex3];

            gridPoint = rootRotation * gridPoint;
            weighedOffset += boneCell.weight0 * (float3)(boneRotations[boneCell.boneIndex0] * gridPoint);
            weighedOffset += boneCell.weight1 * (float3)(boneRotations[boneCell.boneIndex1] * gridPoint);
            weighedOffset += boneCell.weight2 * (float3)(boneRotations[boneCell.boneIndex2] * gridPoint);
            weighedOffset += boneCell.weight3 * (float3)(boneRotations[boneCell.boneIndex3] * gridPoint);

            targetPos = weighedPoint + weighedOffset;
            position[index] = new Position { Value = targetPos };
            //if (math.length(targetPos - position[index].Value) <= .2f)
            //{
            //    position[index] = new Position { Value = targetPos };
            //    velocity[index] = new Velocity { Value = float3.zero };
            //}
            //else
            //{
            //    //float3 newVel = math.lerp(velocity[index].Value, targetPos - position[index].Value, .5f);
            //    float3 newVel = targetPos - position[index].Value;
            //    float lengthSQ = math.lengthsq(newVel);
            //    if (lengthSQ < minVel * minVel)
            //        newVel = minVel * math.normalize(newVel);
            //    else if (lengthSQ > maxVel * maxVel)
            //        newVel = maxVel * math.normalize(newVel);

            //    velocity[index] = new Velocity { Value = newVel };
            //}

        }
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        if(_targetIndices.IsCreated)
            _targetIndices.Dispose();

        if (_targetPoints.IsCreated)
            _targetPoints.Dispose();

        if(_bonePositions.IsCreated)
            _bonePositions.Dispose();

        if (_boneRotations.IsCreated)
            _boneRotations.Dispose();

        if (_boneInverseBaseRotations.IsCreated)
            _boneInverseBaseRotations.Dispose();

        if(_boneWeights.IsCreated)
            _boneWeights.Dispose();
    }

    private void LoadModelFormationData(ColliderGrid grid)
    {
        _currentGrid = grid;
        if (_targetPoints.IsCreated)
            _targetPoints.Dispose();

        _targetPoints = new NativeArray<float3>(grid.PointList.Count, Allocator.Persistent);

        for (int i = 0; i < grid.PointList.Count; i++)
            _targetPoints[i] = grid.PointList[i];
    }

    private void LoadSkinnedModelFormationData(ColliderGrid grid)
    {
        if (grid != _currentGrid)
        {
            LoadModelFormationData(grid);
            if (_boneWeights.IsCreated)
            {
                _boneInverseBaseRotations.Dispose();
                _boneWeights.Dispose();
            }

            _boneWeights = new NativeArray<BoneWeight>(grid.BoneWeightIndices.Count, Allocator.Persistent);
            _boneInverseBaseRotations = new NativeArray<Quaternion>(grid.BoneList.Count, Allocator.Persistent);

            Mesh mesh = grid._skinnedMeshRenderer.sharedMesh;
            BoneWeight [] weights = mesh.boneWeights;
            List<int> boneWeightIndices = grid.BoneWeightIndices;
            for (int i = 0; i < grid.BoneWeightIndices.Count; i++)
            {
                int index = boneWeightIndices[i];
                _boneWeights[i] = weights[index];
            }
            grid._skinnedMeshRenderer.sharedMesh = null;

            for (int i = 0; i < grid.BoneList.Count; i++)
                _boneInverseBaseRotations[i] = Quaternion.Inverse(grid.BoneList[i].rotation);
            

        }
        else if(_bonePositions.IsCreated)
        {            
            _boneRotations.Dispose();
            _bonePositions.Dispose();
        }

        _bonePositions = new NativeArray<float3>(grid.BoneList.Count, Allocator.TempJob);
        _boneRotations = new NativeArray<Quaternion>(grid.BoneList.Count, Allocator.TempJob);
        for (int i = 0; i < grid.BoneList.Count; i++)
        {
            var transform = grid.BoneList[i];

            _bonePositions[i] = transform.position;
            _boneRotations[i] = transform.rotation * _boneInverseBaseRotations[i];
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (_targetIndices.IsCreated)
            _targetIndices.Dispose();

        _targetIndices = new NativeArray<int>(_swarmData.Length, Allocator.TempJob);

        //TODO work on multiple targets
        //TODO clean this shit up
        foreach(var entity in GetEntities<GridPoints>())
        {
            var grid = entity.grid;
            bool isSkinned = grid.IsSkinnedMesh;
            
            if (grid.IsSkinnedMesh)
                LoadSkinnedModelFormationData(grid);
            else
                LoadModelFormationData(grid);

            int targetNodeCount = _currentGrid.IsSkinnedMesh ? _boneWeights.Length : _targetPoints.Length;
            int offset = (int)math.ceil((float)targetNodeCount / (float)_swarmData.Length);
            int count = 0;

            for (int i = 0; i < targetNodeCount; i += offset)
            {
                if (count >= _swarmData.Length)
                    break;

                _targetIndices[count++] = i;
            }

            if (_currentGrid.IsSkinnedMesh)
            {
                return new SwarmSkinnedModelJob
                {
                    Length = _swarmData.Length,
                    position = _swarmData.position,
                    rotation = _swarmData.rotation,
                    velocity = _swarmData.velocity,
                    targetIndices = _targetIndices,
                    cellPosition = _targetPoints,
                    boneGridCells = _boneWeights,
                    bonePositions = _bonePositions,
                    boneRotations = _boneRotations,
                    rootPosition = grid.rootArmature.position,
                    rootRotation = grid.rootArmature.rotation,
                    rootScale = grid.gameObject.transform.localScale,
                    minVel = Bootstrap.settings._minVel,
                    maxVel = Bootstrap.settings._maxVel,
                    deltaT = Time.deltaTime
                }.Schedule(_swarmData.Length, 32, inputDeps);
            }
            else
            {
                return new SwarmModelJob
                {
                    Length = _swarmData.Length,
                    position = _swarmData.position,
                    rotation = _swarmData.rotation,
                    velocity = _swarmData.velocity,
                    targetIndices = _targetIndices,
                    targetPositions = _targetPoints,
                    rootPosition = grid.rootArmature.position,
                    rootRotation = grid.rootArmature.rotation,
                    rootScale = grid.gameObject.transform.localScale,
                    minVel = Bootstrap.settings._minVel,
                    maxVel = Bootstrap.settings._maxVel,
                    deltaT = Time.deltaTime
                }.Schedule(_swarmData.Length, 32, inputDeps);
            }
        }

        return inputDeps;
    }
}
