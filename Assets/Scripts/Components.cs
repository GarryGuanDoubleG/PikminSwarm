using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public struct Velocity :IComponentData
{
    public float3 Value;
}

public struct TargetComponent : IComponentData{ }
public struct Player : IComponentData { }
public struct Pikmin : IComponentData { }

public struct SwarmModelFormation : IComponentData { }
public struct SwarmFlockFormation : IComponentData { }