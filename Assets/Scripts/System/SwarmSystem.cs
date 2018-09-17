﻿using System.Collections;
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
        public ComponentDataArray<Velocity> velocity;        

    }
    struct PlayerData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Player> player;
    }

    struct CalculateSwarmVelocityJob : IJob
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;        
        [ReadOnly] public float3 targetPosition;

        [WriteOnly] public NativeArray<float3> result;
        public void Execute()
        {
            float3 alignment = float3.zero;
            float3 cohesion = targetPosition;

            for(int i = 0; i < Length; i++)
            {
                alignment += velocity[i].Value;
                cohesion += position[i].Value;
            }

            alignment /= (float)Length;
            cohesion /= (float)Length + 1.0f;
            result[0] = alignment;
            result[1] = cohesion;
        }
    }

    struct FlockJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Position> pikPosition;
        public ComponentDataArray<Velocity> pikVelocity;

        [ReadOnly] public float3 target;        
        [ReadOnly] public float3 alignment;
        [ReadOnly] public float3 cohesion;

        [ReadOnly] public float separationDist;
        [ReadOnly] public float minVel;
        [ReadOnly] public float maxVel;

        [ReadOnly] public float deltaT;
        private float3 CalcVelocity(float3 position, float3 velocity)
        {            
            float3 align = alignment - velocity;
            float3 center = cohesion - position;
            float3 seek = target - position;
            float3 separation = float3.zero;

            float distSQ = math.lengthsq(seek);
            float sepDistSQ = separationDist * separationDist;
            if(distSQ < sepDistSQ)
            {
                separation = -seek;
            }

            return align + center + separation;
        }

        public void Execute(int i)
        {            
            float3 newVel = math.lerp(pikVelocity[i].Value, CalcVelocity(pikPosition[i].Value, pikVelocity[i].Value), math.exp(-24.0f * deltaT));
            float speed = math.lengthsq(newVel);
            if (speed > maxVel * maxVel)
                newVel = math.normalize(newVel) * maxVel;
            else if (speed < minVel * minVel)
                newVel = math.normalize(newVel) * minVel;

            pikVelocity[i] = new Velocity { Value = newVel };
        }
    }

    [Inject] PlayerData _playerData;
    [Inject] PikminData _pikminData;
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeArray<float3> result = new NativeArray<float3>(2, Allocator.TempJob);
        var SwarmControllerJob = new CalculateSwarmVelocityJob
        {
            Length = _pikminData.Length,
            position = _pikminData.position,
            velocity = _pikminData.velocity,
            targetPosition = _playerData.position[0].Value,
            result = result
        };
        var jobHandler = SwarmControllerJob.Schedule(inputDeps);
        jobHandler.Complete();

        var flockJob = new FlockJob
        {
            Length = _pikminData.Length,
            pikPosition = _pikminData.position,
            pikVelocity = _pikminData.velocity,
            target = _playerData.position[0].Value,
            alignment = result[0],
            cohesion = result[1],
            separationDist = 10.0f,
            minVel = 25.0f,
            maxVel = 50.0f,
            deltaT = Time.deltaTime
        };
        result.Dispose();
        return flockJob.Schedule(_pikminData.Length, 64, jobHandler);        
    }
}