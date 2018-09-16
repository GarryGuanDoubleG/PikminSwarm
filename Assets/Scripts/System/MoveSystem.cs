using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;

public class MoveSystem : JobComponentSystem
{
    struct PlayerData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;
        public ComponentDataArray<Position> position;        
    }

    [BurstCompile]
    struct MoveJob : IJobParallelFor
    {
        [ReadOnly] public int Length;
        [ReadOnly] public float dt;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;
        [ReadOnly] public ComponentDataArray<Position> position;

        public NativeArray<Position> newPositions;
        public void Execute(int i)
        {
            newPositions[i] = new Position { Value = position[i].Value + velocity[i].Value * dt};
        }
    }

    [Inject] PlayerData m_data;
    // Use this for initialization
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeArray<Position> posArr = new NativeArray<Position>(m_data.Length, Allocator.Temp);
        var moveJob = new MoveJob
        {
            Length = m_data.Length,
            dt = Time.deltaTime,
            velocity = m_data.velocity,
            position = m_data.position,
            newPositions = posArr
        };

        var handler = moveJob.Schedule(m_data.Length, 64, inputDeps);
        handler.Complete();

        for (int i = 0; i < m_data.Length; i++)
            m_data.position[i] = posArr[i];

        posArr.Dispose();
        return inputDeps;
    }
}
