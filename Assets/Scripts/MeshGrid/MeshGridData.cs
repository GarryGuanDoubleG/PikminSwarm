using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SerializeField]
public class MeshGridData
{
    public List<Vector3> points;//offset from the bone
    public List<int> boneIndices;//index of the bone the point attaches to
    public Vector3 cellSize;
    public GameObject[] boneReferences;    
    //TODO assign points to bones
}
