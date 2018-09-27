using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class SwarmModelSystem : JobComponentSystem
{
    [Inject] SwarmData _swarmData;

    NativeArray<int> _targetIndices;
    NativeArray<float3> _targetPoints;
    NativeArray<float3> _bonePositions;
    NativeArray<float> _randomOffsets;
    NativeArray<int> _attachedToTarget; //TODO make this into bitarrays
    NativeArray<BoneWeight> _boneWeights;
    NativeArray<Quaternion> _boneRotations;
    NativeArray<Quaternion> _boneInverseBaseRotations;
        
    ColliderGrid _currentGrid;

    float _timer;

	struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<SwarmModelFormation> swarm;

        public ComponentDataArray<Position> position;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;
    }

    struct TargetModel
    {
        public ColliderGrid grid;
    }

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
        [ReadOnly] public NativeArray<float> randomOffsets;
        [ReadOnly] public NativeArray<BoneWeight> boneGridCells;
        [ReadOnly] public NativeArray<float3> bonePositions;
        [ReadOnly] public NativeArray<Quaternion> boneRotations;

        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;
        [ReadOnly] public float deltaT;
        [ReadOnly] public float time;
        [ReadOnly] public float minDistSQ;
        [ReadOnly] public float swarmTime;
        [ReadOnly] public float timer;
        [ReadOnly] public float swarmRotationSpeed;
        [ReadOnly] public float swarmTimeOffsetFactor;
        [ReadOnly] public float swarmOffsetDistance;
        [ReadOnly] public float minSwarmRotationSpeed;

        public NativeArray<int> attachedToTarget;

        public ComponentDataArray<Position> position;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

        public float3 GetNewVelocity(float3 targetPos, int index)
        {
            float rotSpeed = swarmRotationSpeed < minSwarmRotationSpeed ? minSwarmRotationSpeed : swarmRotationSpeed;
            float3 currVel = velocity[index].Value;
            float speed = math.length(currVel);
            float3 newVel;
            float completion = math.abs(timer) / swarmTime;
            if (timer < 0)
                newVel = (1.0f - completion) * currVel + math.lerp(currVel, speed * math.normalize(targetPos - position[index].Value), completion);
            else
                newVel = currVel + math.lerp(currVel, targetPos - position[index].Value, math.exp(rotSpeed * -deltaT));

            float velSQ = math.lengthsq(newVel);
            if (velSQ < minVel * minVel)
                newVel = minVel * math.normalize(newVel);
            else if (velSQ > maxVel * maxVel)
                newVel = maxVel * math.normalize(newVel);

            return newVel;
        }

        public void GetNewRotation(float3 dir, out Quaternion newRot)
        {
            float3 up = math.up();
            float3 right = math.cross(dir, up);
            up = math.cross(dir, right);
            float3 newDir = Quaternion.FromToRotation(up, dir) * dir; // get the top of the capsule to face the flying direction

            newRot =  Quaternion.LookRotation(newDir, dir);
        }

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
            if (boneCell.weight1 > 0.01f)
                weighedOffset += boneCell.weight1 * (float3)(boneRotations[boneCell.boneIndex1] * gridPoint);
            if (boneCell.weight2 > 0.01f)
                weighedOffset += boneCell.weight2 * (float3)(boneRotations[boneCell.boneIndex2] * gridPoint);
            if (boneCell.weight3 > 0.01f)
                weighedOffset += boneCell.weight3 * (float3)(boneRotations[boneCell.boneIndex3] * gridPoint);

            targetPos = weighedPoint + weighedOffset;
            float3 toRootDir = math.normalize(targetPos - rootPosition);
            float sinWave = math.clamp(math.sin(swarmTimeOffsetFactor * time), 0, 1.0f);
            targetPos += toRootDir * randomOffsets[targetIndex] * sinWave * swarmOffsetDistance;

            int attached = attachedToTarget[index];
            float distanceSQ = math.lengthsq(targetPos - position[index].Value);
            if (swarmRotationSpeed <= 0.0f && (attachedToTarget[index] == 1 || math.lengthsq(targetPos - position[index].Value) <= minDistSQ))
            {
                attachedToTarget[index] = 1;
                position[index] = new Position { Value = targetPos };

                if (math.lengthsq(velocity[index].Value) != 0.0f)
                    velocity[index] = new Velocity { Value = float3.zero };
            }
            else
            {
                attachedToTarget[index] = 0;

<<<<<<< Updated upstream
                float rotSpeed = swarmRotationSpeed < minSwarmRotationSpeed ? minSwarmRotationSpeed : swarmRotationSpeed;
                float3 currVel = velocity[index].Value;
                float speed = math.length(currVel);
                float3 newVel;
                float completion = math.abs(timer) / swarmTime;
                if(timer < -swarmTime * .33f)
                    newVel = (1.0f - completion) * currVel + math.lerp(currVel, speed * math.normalize(targetPos - position[index].Value), completion);
                else
                    newVel = currVel + math.lerp(currVel, targetPos - position[index].Value, math.exp(rotSpeed * -deltaT));

                float velSQ = math.lengthsq(newVel);
                if (velSQ < minVel * minVel)
                    newVel = minVel * math.normalize(newVel);
                else if (velSQ > maxVel * maxVel)
                    newVel = maxVel * math.normalize(newVel);

                float3 dir = math.normalize(newVel);
                float3 up = math.up();
                float3 right = math.cross(dir, up);
                up = math.cross(dir, right);
                float3 newDir = Quaternion.FromToRotation(up, dir) * dir; // get the top of the capsule to face the flying direction

                rotation[index] = new Rotation { Value = Quaternion.LookRotation(newDir, dir) };
=======
                float3 newVel = GetNewVelocity(targetPos, index);
                Quaternion newRot;
                GetNewRotation(math.normalize(newVel), out newRot);
                rotation[index] = new Rotation { Value = newRot };            
>>>>>>> Stashed changes
                velocity[index] = new Velocity { Value = newVel };
            }

        }
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();

        _timer = Bootstrap.settings._swarmTime;
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

        if (_randomOffsets.IsCreated)
            _randomOffsets.Dispose();
        if (_attachedToTarget.IsCreated)
            _attachedToTarget.Dispose();
        //if (_positionOffsets.IsCreated)
        //    _positionOffsets.Dispose();
    }

    private void LoadModelFormationData(ColliderGrid grid)
    {        
        if (_currentGrid != grid)
        {
            if (_targetPoints.IsCreated)
                _targetPoints.Dispose();

            if (_randomOffsets.IsCreated)
                _randomOffsets.Dispose();
            if (_attachedToTarget.IsCreated)
                _attachedToTarget.Dispose();

            _targetPoints = new NativeArray<float3>(grid.PointList.Count, Allocator.Persistent);
            _randomOffsets = new NativeArray<float>(grid.PointList.Count, Allocator.Persistent);
            _attachedToTarget = new NativeArray<int>(_swarmData.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < grid.PointList.Count; i++)
            {
                _targetPoints[i] = grid.PointList[i];
                _randomOffsets[i] = Mathf.PerlinNoise(_targetPoints[i].x, _targetPoints[i].y);
            }

            _currentGrid = grid;
        }
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
            //get rid of the mesh so it stops rendering without stopping the animator
            grid._skinnedMeshRenderer.sharedMesh = null;

            for (int i = 0; i < grid.BoneList.Count; i++)
                _boneInverseBaseRotations[i] = Quaternion.Inverse(grid.BoneList[i].rotation);

            grid._animator.enabled = true;
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
        //_positionOffsets = new NativeArray<float3>(_swarmData.Length, Allocator.TempJob);
        //TODO work on multiple targets
        //TODO clean this shit up
        foreach(var entity in GetEntities<TargetModel>())
        {
            var grid = entity.grid;
            if (grid != _currentGrid)
                _timer = Bootstrap.settings._swarmTime;
            
            if (grid.IsSkinnedMesh)
                LoadSkinnedModelFormationData(grid);
            else
                LoadModelFormationData(grid);

            int targetNodeCount = _targetPoints.Length;
            int indexOffset = (int)math.ceil((float)targetNodeCount / (float)_swarmData.Length);
            for (int i = 0; i < _swarmData.Length; i++)
                _targetIndices[i] = (i * indexOffset) % targetNodeCount;

            if (_currentGrid.IsSkinnedMesh)
            {
                float rotationSpeed = Bootstrap.settings._swarmRotationSpeed;
                float minDist = _timer * _timer;                
                float timedMinDistSQ = minDist * minDist;

                if (timedMinDistSQ > 150.0f)
                    timedMinDistSQ = 150.0f;

                _timer -= Time.deltaTime;

                //if (timedMinDistSQ > 200.0f) timedMinDistSQ = 200.0f;
                if (_timer <= 0.0f)
                {
                    rotationSpeed = 0.0f;

                    if (_timer <= -Bootstrap.settings._swarmTime)
                    {
                        _timer = Bootstrap.settings._swarmTime * .5f;

                        for (int i = 0; i < _swarmData.Length; i++)
                            _attachedToTarget[i] = 0;
                    }
                }
                else
                {                    
                    rotationSpeed *= (_timer / Bootstrap.settings._swarmTime);
                }

                return new SwarmSkinnedModelJob
                {
                    Length = _swarmData.Length,
                    position = _swarmData.position,
                    rotation = _swarmData.rotation,
                    velocity = _swarmData.velocity,
                    targetIndices = _targetIndices,
                    attachedToTarget = _attachedToTarget,
                    randomOffsets = _randomOffsets,
                    cellPosition = _targetPoints,
                    boneGridCells = _boneWeights,
                    bonePositions = _bonePositions,
                    boneRotations = _boneRotations,
                    rootPosition = grid.rootArmature.position,
                    rootRotation = grid.rootArmature.rotation,
                    rootScale = grid.gameObject.transform.localScale,
                    minVel = Bootstrap.settings._minVel,
                    maxVel = Bootstrap.settings._maxVel,
                    minDistSQ = timedMinDistSQ,
                    swarmTimeOffsetFactor = Bootstrap.settings._swarmOffsetTimeFactor,
                    swarmOffsetDistance = Bootstrap.settings._swarmOffsetDistance,
                    swarmRotationSpeed = rotationSpeed,
                    minSwarmRotationSpeed = Bootstrap.settings._minSwarmRotationSpeed,
                    swarmTime = Bootstrap.settings._swarmTime,
                    timer = _timer,
                    deltaT = Time.deltaTime,
                    time = Time.time
                }.Schedule(_swarmData.Length, 1024, inputDeps);
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
                }.Schedule(_swarmData.Length, 1024, inputDeps);
            }
        }

        return inputDeps;
    }
}
