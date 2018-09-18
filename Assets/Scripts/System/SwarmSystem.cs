using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

public class SwarmSystem : JobComponentSystem
{    
    struct PikminData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;

    }
    struct PlayerData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Player> player;
    }

    struct SwarmData
    {
        public bool active;
        public NativeArray<float3> alignArr;
        public NativeArray<float3> cohesionArr;
        public NativeArray<float3> separationArr;
    }

    struct CalculateSwarmVelocityJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Position> pikPosition;
        [ReadOnly] public ComponentDataArray<Velocity> pikVelocity;        
        [ReadOnly] public float3 targetPosition;

        [ReadOnly] public float neighborDist;
        [ReadOnly] public float separationDist;

        [WriteOnly] public NativeArray<float3> alignArr;
        [WriteOnly] public NativeArray<float3> cohesArr;
        [WriteOnly] public NativeArray<float3> separtArr;
        
        public void Execute(int index)
        {
            float3 alignment = float3.zero;
            float3 cohesion = targetPosition;
            float3 separation = float3.zero;

            int neighborCount = 1;

            for(int i = 0; i < Length; i++)
            {
                if (i == index) continue;
                float3 diff = pikPosition[index].Value - pikPosition[i].Value;
                float dist = math.length(diff);

                if(dist <= neighborDist)
                {
                    alignment += pikVelocity[i].Value;
                    cohesion += pikPosition[i].Value;
                    float scaler = math.clamp(1.0f - dist / neighborDist, 0.0f, 1.0f);
                    separation += diff * (scaler / dist);
                    neighborCount++;
                }
            }

            alignArr[index] = alignment / (float)neighborCount;
            cohesArr[index] = cohesion / (float)neighborCount;
            separtArr[index] = separation;
        }
    }

    struct FlockJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Position> pikPosition;
        public ComponentDataArray<Velocity> pikVelocity;
        public ComponentDataArray<Rotation> pikRotation;

        [ReadOnly] public float3 target;        
        [ReadOnly] public NativeArray<float3> alignArr;
        [ReadOnly] public NativeArray<float3> cohesionArr;
        [ReadOnly] public NativeArray<float3> separationArr;

        [ReadOnly] public float neighborDist;
        [ReadOnly] public float separationDist;
        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;

        [ReadOnly] public float deltaT;
        [ReadOnly] public float swarmRotSpeed;

        [ReadOnly] public float sepWeight, 
                                alignWeight, 
                                cohesionWeight, 
                                seekWeight;

        private float3 CalcVelocity(float3 position, float3 velocity, int index)
        {            
            float3 align = alignArr[index] - velocity;
            float3 center = cohesionArr[index] - position;
            float3 seek = target - position;
            float3 separation = separationArr[index];

            if (math.lengthsq(separation) > 0)
                separation = math.normalize(separation);

            return  alignWeight * align + 
                    cohesionWeight * center +
                    sepWeight * separation + 
                    seekWeight * seek;
        }

        public void Execute(int i)
        {
            float3 newVel = math.lerp(pikVelocity[i].Value, CalcVelocity(pikPosition[i].Value, pikVelocity[i].Value, i), math.exp(-swarmRotSpeed * deltaT));
            float speed = math.lengthsq(newVel);
            float3 dir = math.normalize(newVel);
            if (speed > maxVel * maxVel)
                newVel = dir * maxVel;
            else if (speed < minVel * minVel)
                newVel = dir * minVel;

            float3 newDir = Quaternion.Euler(90.0f, 0, 0) * dir;//make the head face the direction
            
            pikVelocity[i] = new Velocity { Value = newVel };
            pikRotation[i] = new Rotation { Value = Quaternion.LookRotation(newDir, dir) };
        }
    }

    [Inject] PlayerData _playerData;
    [Inject] PikminData _pikminData;
    SwarmData _swarmData;
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        int pikCount = _pikminData.Length;

        if(_swarmData.active)
        {
            _swarmData.alignArr.Dispose();
            _swarmData.cohesionArr.Dispose();
            _swarmData.separationArr.Dispose();
        }
        
        NativeArray<float3> alignArray = new NativeArray<float3>(pikCount, Allocator.TempJob);
        NativeArray<float3> cohesionArray= new NativeArray<float3>(pikCount, Allocator.TempJob);
        NativeArray<float3> separationArray = new NativeArray<float3>(pikCount, Allocator.TempJob);

        _swarmData.active = true;
        _swarmData.alignArr = alignArray;
        _swarmData.cohesionArr = cohesionArray;
        _swarmData.separationArr = separationArray;

        var SwarmControllerJob = new CalculateSwarmVelocityJob
        {
            Length = _pikminData.Length,
            pikPosition = _pikminData.position,
            pikVelocity = _pikminData.velocity,
            targetPosition = _playerData.position[0].Value,
            neighborDist = Bootstrap.settings._neighborDist,
            separationDist = Bootstrap.settings._separationDist,
            alignArr = alignArray,
            cohesArr = cohesionArray,
            separtArr = separationArray
        };
        var jobHandler = SwarmControllerJob.Schedule(pikCount, 64, inputDeps);
        jobHandler.Complete();

        var flockJob = new FlockJob
        {
            Length = _pikminData.Length,
            pikPosition = _pikminData.position,
            pikVelocity = _pikminData.velocity,
            pikRotation = _pikminData.rotation,
            target = _playerData.position[0].Value,
            alignArr = alignArray,
            cohesionArr = cohesionArray,
            separationArr = separationArray,
            neighborDist = Bootstrap.settings._neighborDist,
            separationDist = Bootstrap.settings._separationDist,
            minVel = Bootstrap.settings._minVel,
            maxVel = Bootstrap.settings._maxVel,
            deltaT = Time.deltaTime,
            swarmRotSpeed = Bootstrap.settings._swarmRotationSpeed,
            alignWeight = Bootstrap.settings._alignWeight,
            cohesionWeight = Bootstrap.settings._cohesionWeight,
            sepWeight = Bootstrap.settings._separationWeight,
            seekWeight = Bootstrap.settings._seekWeight
        };

        return flockJob.Schedule(_pikminData.Length, 64, jobHandler);        
    }
}
