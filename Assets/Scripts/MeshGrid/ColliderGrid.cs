using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class ColliderGrid : MonoBehaviour
{
    public GameObject rootObject;
    public GameObject rootMesh;
    public Transform rootArmature;

    List<Transform> _boneList;
    public List<Transform> BoneList
    {
        get { return _boneList; }
        private set { _boneList = value; }
    }

    List<Vector3> _bonePointList;
    public List<Vector3> BonePointList
    {
        get { return _bonePointList; }
        private set { _bonePointList = value; }
    }

    List<int> _boneIndexList;
    public List<int> BoneIndexList
    {
        get { return _boneIndexList; }
        private set { _boneIndexList = value; }
    }

    public TextAsset bakedPoints;

    private void Awake()
    {
        _boneList = new List<Transform>();
        GetBoneList(rootArmature);
    }

    // Use this for initialization
    void Start()
    {
        if (bakedPoints == null)
            Debug.LogError("Baked points cannot be empty");

        MeshGridData meshGrid = JsonUtility.FromJson<MeshGridData>(bakedPoints.text);
        _bonePointList = meshGrid.offsets;
        _boneIndexList = meshGrid.boneIndices;
    }

    void GetBoneList(Transform transform)
    {
        int childCount = transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                _boneList.Add(child);
                GetBoneList(child);
            }
        }
    }
}