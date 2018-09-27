using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class Octree : MonoBehaviour
{
    //private static int MAX_ENTITES = 20;
    public static float MIN_SIZE = .5f;
    private float3 _minPos;
    private float _size;
    private Octree [] _children;

    public bool active;

    public List<float3> _position;
    public List<float3> _velocity;
    public List<int> _entityID;


    public Octree()
    {
    }
    public Octree(float3 min, float nodeSize)
    {
        _minPos = min;
        _size = nodeSize;
    }    
    
    public bool ContainsPoint(float3 position)
    {
        float3 max = _minPos + new float3(_size);        
        return position.x >= _minPos.x && position.y >= _minPos.y && position.z >= _minPos.z &&
                position.x <= max.x && position.y <= max.y && position.z <= max.z;
    }
    
}
