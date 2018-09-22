using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Jobs;
using System.IO;

public class ColliderGridGenerator : MonoBehaviour
{
    public MeshCollider _meshCollider;
    public BoxCollider[] _meshBoxColliderArr;

    public GameObject _rootObject;
    public GameObject _rootMesh;
    public GameObject _armatureRoot;
    private List<Transform> _boneList;
    private List<int> _boneIndices;

    public ColliderCell _gridCellObject;
    public Vector3 _gridCellSize;
    private List<ColliderCell> _cellList;

    List<Vector3> _pointsList;
    public List<Vector3> PointList
    {
        get { return _pointsList; }
        private set { _pointsList = value; }
    }

    List<Vector3> _pointBoneOffsetList;
    public List<Vector3> OffsetList
    {
        get { return _pointBoneOffsetList; }
        private set { _pointBoneOffsetList = value; }
    }

    public int _maxHitCount;
    public bool _generateGrid;    
    public bool _multiDirectionalCheck;
    public MeshGridData _meshGridData;

    public bool _writeToFile;
    public string _path;
    public string _fileName;

    private void Awake()
    {
    }

    // Use this for initialization
    void Start()
    {
        _cellList = new List<ColliderCell>();
        _pointsList = new List<Vector3>();
        _boneList = new List<Transform>();

        GetBoneList(_armatureRoot.transform);

        transform.position = Vector3.zero;
        transform.localScale = Vector3.one;

        if (_generateGrid)
        {
            GenerateGrid();
            GetPointBones();

            _meshGridData = new MeshGridData();
            _meshGridData.offsets = _pointBoneOffsetList;
            _meshGridData.cellSize = _gridCellSize;
            _meshGridData.boneIndices = _boneIndices;

            SpawnColliders();            

            if (_writeToFile)
            {
                string json = JsonUtility.ToJson(_meshGridData);
                StreamWriter writer = new StreamWriter(_path + _fileName, false);
                writer.WriteLine(json);
                writer.Close();
            }            
        }
    }

    void GetBoneList(Transform transform)
    {
        int childCount = transform.childCount;
        for(int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                _boneList.Add(child);
                GetBoneList(child);
            }
        }
    }

    void GenerateGrid()
    {
        Vector3 scale = _rootMesh.transform.localScale;
        foreach (var meshBoxCollider in _meshBoxColliderArr)
        {
            Vector3 boxColliderSize = meshBoxCollider.size;
            Vector3 worldSize = Vector3.Scale(meshBoxCollider.size, scale);
            Vector3 offset = Vector3.Scale(meshBoxCollider.center, scale) - worldSize * .5f;

            Vector3 cellsPerSide = new Vector3(worldSize.x / _gridCellSize.x, worldSize.y / _gridCellSize.y, worldSize.z / _gridCellSize.z);
            Vector3 halfCellSize = _gridCellSize * .5f;

            for (int x = 0; x <= (int)cellsPerSide.x; x++)
            {
                for (int y = 0; y <= (int)cellsPerSide.y; y++)
                {
                    for (int z = 0; z <= (int)cellsPerSide.z; z++)
                    {
                        Vector3 pos = halfCellSize + (new Vector3(x * _gridCellSize.x, y * _gridCellSize.y, z * _gridCellSize.z));
                        pos += offset;

                        if (IsInsideTest(pos, Vector3.forward))
                            PointList.Add(pos);
                    }
                }
            }
        }

        if (_multiDirectionalCheck)
            ReverseCollisionCheck();
    }

    //find which bones to attach each point to
    void GetPointBones()
    {
        _boneIndices = new List<int>(_pointsList.Count);
        _pointBoneOffsetList = new List<Vector3>(_pointsList.Count);
        for(int i = 0; i < _pointsList.Count; i++)
        {
            float minDistSQ = 99999.0f;
            int boneIndex = 0;
            int minBoneIndex = -1;
            Vector3 point = _pointsList[i];
            foreach(var trans in _boneList)
            {
                Vector3 bonePos = trans.position;
                Vector3 distance = point - bonePos;
                float distSQ = Vector3.Dot(distance, distance);
                if (distSQ < minDistSQ)
                {
                    minBoneIndex = boneIndex;
                    minDistSQ = distSQ;
                }
                boneIndex++;
            }

            Vector3 bonePosition = _boneList[minBoneIndex].transform.position;
            Vector3 offset = _pointsList[i] - bonePosition;

            _pointBoneOffsetList.Add(offset);
            _boneIndices.Add(minBoneIndex);
        }
    }

    public bool IsInsideTest(Vector3 cellPoint, Vector3 goalDir)
    {
        int hitCount = 0;
        float maxDistance = 1000.0f;
        Vector3 goal = cellPoint + goalDir * maxDistance;
        Vector3 currPoint;
        currPoint = cellPoint;
        Vector3 dir = Vector3.Normalize(goal - currPoint);
        float hitPointOffset = 0.1f;

        RaycastHit hit;
        //basicallly do a raycast in a single direction and see how many times you hit the mesh
        //then do it again back to the cell point to get any back facing meshes

        while (currPoint != goal && hitCount <= _maxHitCount)
        {
            if (Physics.Linecast(currPoint, goal, out hit) && hit.collider == _meshCollider)
            {
                hitCount++;
                currPoint = hit.point + dir * hitPointOffset;
            }
            else
                currPoint = goal;
        }
        while (currPoint != cellPoint && hitCount <= _maxHitCount)
        {
            if (Physics.Linecast(currPoint, cellPoint, out hit) && hit.collider == _meshCollider)
            {
                hitCount++;
                currPoint = hit.point + -dir * hitPointOffset;
            }
            else
                currPoint = cellPoint;
        }


        return (hitCount % 2) != 0 && hitCount <= _maxHitCount;

    }

    void SpawnColliders()
    {
        GameObject[] gameObjArr = new GameObject[PointList.Count];
        int count = 0;
        foreach (var point in PointList)
        {
            GameObject cell = Instantiate(_gridCellObject.gameObject, _rootObject.transform);
            gameObjArr[count] = cell;
            cell.transform.localPosition = point;

            ColliderCell cellCollider = cell.GetComponent<ColliderCell>();
            cellCollider.cellCollider.size = _gridCellSize;
            _cellList.Add(cell.GetComponent<ColliderCell>());
            count++;
        }
    }

    void ReverseCollisionCheck()
    {
        List<Vector3> checkPointList = new List<Vector3>(_pointsList.Count);
        foreach (Vector3 point in PointList)
        {
            if (IsInsideTest(point, -Vector3.forward))
                checkPointList.Add(point);
        }
        PointList = checkPointList;
    }

    // Update is called once per frame
    void Update()
    {

    }
}