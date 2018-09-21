using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class ColliderGrid : MonoBehaviour
{
    public GameObject rootObject;
    public GameObject rootMesh;

    List<Vector3> pointsList;
    public List<Vector3> PointList
    {
        get { return pointsList; }
        private set { pointsList = value; }
    }

    public TextAsset bakedPoints;

    // Use this for initialization
    void Start()
    {
        if (bakedPoints == null)
            Debug.LogError("Baked points cannot be empty");
        
        pointsList = JsonUtility.FromJson<MeshGridData>(bakedPoints.text).points;
    }
}