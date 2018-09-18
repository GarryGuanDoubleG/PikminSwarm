using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
public class Settings : MonoBehaviour {

    public int _spawnCount;

    [Header("Boid Flocking Weights")]
    public float _alignWeight;
    public float _cohesionWeight;
    public float _separationWeight;
    public float _seekWeight;

    [Header("Boid Swarm Values")]
    public float _swarmRotationSpeed;
    public float _neighborDist;
    public float _separationDist;
    public float _minVel;
    public float _maxVel;


}
