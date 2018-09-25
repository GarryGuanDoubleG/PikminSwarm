using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

public class Settings : MonoBehaviour {

    [Header("Spawn")]
    public int _spawnCount;
    public float3 _spawnScale;

    [Header("Prefabs")]
    public List<MeshInstanceRendererComponent> _pikminLook;
    public MeshInstanceRendererComponent _playerLook;

    [Header("Boid Flocking Weights")]
    public float _alignWeight;
    public float _cohesionWeight;
    public float _separationWeight;
    public float _seekWeight;

    [Header("Boid Swarm Values")]
    public float _swarmRotationSpeed;
    public float _minSwarmRotationSpeed;
    public float _neighborDist;
    public float _minVel;
    public float _maxVel;

    [Header("Boid Swarm Model Formation")]
    public float _swarmOffsetTimeFactor;
    public float _swarmOffsetDistance;
    public float _swarmTime;
    public float _swarmModelStartDelay;
}
