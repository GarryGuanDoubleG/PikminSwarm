using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SerializeField]
public class MeshGridData
{
    public List<Vector3> offsets;//offset from the bone
    public List<int> boneIndices;//index of the bone the point attaches to
    public Vector3 cellSize;
}
