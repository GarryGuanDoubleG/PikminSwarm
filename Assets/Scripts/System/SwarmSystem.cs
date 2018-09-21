using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

public class SwarmSystem : ComponentSystem
{
    [Inject] PikminData _pikminData;
    float _modelFormationTimer;

    struct PikminData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        public ComponentDataArray<SwarmFlockFormation> flock;
        public ComponentDataArray<Rotation> rotation;
        public ComponentDataArray<Velocity> velocity;
        public EntityArray entityArray;
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        _modelFormationTimer = Bootstrap.settings._swarmModelStartDelay;
    }

    protected override void OnUpdate()
    {
        _modelFormationTimer -= Time.deltaTime;
        if (_modelFormationTimer <= 0.0f)
        {
            var entityArr = _pikminData.entityArray;
            for(int i = 0; i < _pikminData.Length; i++)
            {
                PostUpdateCommands.AddComponent<SwarmModelFormation>(entityArr[i], new SwarmModelFormation());
                PostUpdateCommands.RemoveComponent(entityArr[i], typeof(SwarmFlockFormation));
            }

            this.Enabled = false;
        }
    }
}
