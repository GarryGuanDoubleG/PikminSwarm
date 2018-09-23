using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BoneGridCell
{
    public Vector3 position;
    public float [] weights;
    public int [] boneIndices;
}

[SerializeField]
public class MeshGridData
{
    public bool isRigged;
    public List<Vector3> points;//points on mesh
    public List<BoneGridCell> pointBones;//index of the bone the point attaches to    
    public Vector3 cellSize;
}
