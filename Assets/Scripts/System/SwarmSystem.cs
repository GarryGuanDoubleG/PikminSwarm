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
    [Inject] SwarmFlockPikminData _swarmFlockPikmin;
    float _modelFormationTimer;
    State _state;
    float3 _swarmScale;

    enum State
    {
        None,
        Flock,
        Model
    }  


    struct SwarmFlockPikminData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        public ComponentDataArray<SwarmFlockFormation> flock;
        public EntityArray entityArray;
    }

    struct PikminData
    {
        public readonly int Length;        
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        public ComponentDataArray<Scale> scale;

        public EntityArray entityArray;
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        _modelFormationTimer = Bootstrap.settings._swarmModelStartDelay;

        _state = State.Flock;
        _swarmScale = Bootstrap.settings._spawnScale;
    }

    protected override void OnUpdate()
    {
        _modelFormationTimer -= Time.deltaTime;
        if (_modelFormationTimer <= 0.0f && _state != State.Model)
        {
            var entityArr = _swarmFlockPikmin.entityArray;
            for(int i = 0; i < _swarmFlockPikmin.Length; i++)
            {
                PostUpdateCommands.RemoveComponent(entityArr[i], typeof(SwarmFlockFormation));
                PostUpdateCommands.AddComponent<SwarmModelFormation>(entityArr[i], new SwarmModelFormation());                
            }
            _state = State.Model;
        }

        float3 settingScale = Bootstrap.settings._spawnScale;
        bool3 isSame = settingScale == _swarmScale;
        if(!isSame.x || !isSame.y || !isSame.z)
        {
            var entityArr = _pikminData.entityArray;
            for (int i = 0; i < _pikminData.Length; i++)
            {
                PostUpdateCommands.SetComponent<Scale>(entityArr[i], new Scale { Value = settingScale });
            }

            _swarmScale = settingScale;
        }

    }
}
