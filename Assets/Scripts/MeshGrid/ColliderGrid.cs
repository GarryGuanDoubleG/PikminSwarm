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
    private bool _isSkinnedMesh;
    public bool IsSkinnedMesh{ get { return _isSkinnedMesh; }}
    public SkinnedMeshRenderer _skinnedMeshRenderer;

    List<Transform> _boneList;
    public List<Transform> BoneList
    {
        get { return _boneList; }
        private set { _boneList = value; }
    }

    List<Vector3> _pointList;
    public List<Vector3> PointList
    {
        get { return _pointList; }
        private set { _pointList = value; }
    }

    List<int> _boneWeightIndices;
    public List<int> BoneWeightIndices
    {
        get { return _boneWeightIndices; }
        private set { _boneWeightIndices = value; }
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
        //GetBoneList(rootArmature);
        GetBoneList();
    }

    // Use this for initialization
    void Start()
    {
        if (bakedPoints == null)
            Debug.LogError("Baked points cannot be empty");

        MeshGridData meshGrid = JsonUtility.FromJson<MeshGridData>(bakedPoints.text);
        _isSkinnedMesh = meshGrid.isRigged;      
        _boneWeightIndices = meshGrid.boneWeightIndices;
        _pointList = meshGrid.points;
    }

    //void GetBoneList(Transform transform)
    //{
    //    int childCount = transform.childCount;
    //    for (int i = 0; i < childCount; i++)
    //    {
    //        Transform child = transform.GetChild(i);
    //        if (child != null)
    //        {
    //            _boneList.Add(child);
    //            GetBoneList(child);
    //        }
    //    }
    //}

    void GetBoneList()
    {
        Transform[] bones = _skinnedMeshRenderer.bones;
        foreach (Transform bone in bones)        
            _boneList.Add(bone);
    }
}