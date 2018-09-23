using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Jobs;
using System.IO;

public class ColliderGridGenerator : MonoBehaviour
{
    public MeshCollider _meshCollider;
    public SkinnedMeshCollider _skinnedMeshCollider;
    public BoxCollider[] _meshBoxColliderArr;

    public GameObject _rootObject;
    public GameObject _rootMesh;
    public GameObject _rootArmature;

    private List<Transform> _boneList;
    private Dictionary<Transform, int> _boneIndexMap;
    private Dictionary<Transform, List<int>> _connectedBoneMap; //bones with the same parent that affect bone position
    //private List<int> _boneIndices;
    private List<BoneGridCell> _pointBoneWeights;

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
    public bool _isSkinnedMesh;
    public bool _generateGrid;
    public bool _showPoints;
    public bool _multiDirectionalCheck;
    public MeshGridData _meshGridData;

    public bool _writeToFile;
    public string _path;
    public string _fileName;

    private void Awake()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if (_isSkinnedMesh)
        {
            if (_skinnedMeshCollider == null)
                Debug.LogError("Must Place Skin Mesh Collider");

            _skinnedMeshCollider.RecalculateMeshCollider();
        }
    }

    // Use this for initialization
    void Start()
    {
        _cellList = new List<ColliderCell>();
        _pointsList = new List<Vector3>();
        _boneList = new List<Transform>();
        _boneIndexMap = new Dictionary<Transform, int>();
        _connectedBoneMap = new Dictionary<Transform, List<int>>();
        _pointBoneWeights = new List<BoneGridCell>();

        if (_isSkinnedMesh)
        {
            GetBonesList(_rootArmature.transform);
            GetConnectedBones(_rootArmature.transform);
        }

        if (_generateGrid)
        {
            GenerateGrid();

            if (_isSkinnedMesh) GetPointBones();
            if (_writeToFile) WriteToFile();
            if(_showPoints) SpawnColliders();
        }
    }

    void WriteToFile()
    {
        _meshGridData = new MeshGridData();
        _meshGridData.isRigged = _isSkinnedMesh;
        _meshGridData.cellSize = _gridCellSize;
        _meshGridData.points = _isSkinnedMesh ? null : _pointsList;
        _meshGridData.pointBones = _isSkinnedMesh ? _pointBoneWeights : null;

        string json = JsonUtility.ToJson(_meshGridData);
        StreamWriter writer = new StreamWriter(_path + _fileName, false);
        writer.WriteLine(json);
        writer.Close();
    }

    void GetBonesList(Transform transform)
    {
        int childCount = transform.childCount;
        for(int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                _boneIndexMap.Add(child, _boneList.Count);
                _boneList.Add(child);
                GetBonesList(child);
            }
        }
    }

    //parse bone names too see if they match
    string [] GetTokens(Transform trans)
    {
        //TODO name splitArr a public var
        string input = trans.name;
        char[] splitArr = new char[] { '_' };
        string inputName = new string(input.Where(c => c != '-' && (c < '0' || c > '9')).ToArray()); //get rid of numbers
        return inputName.Split(splitArr);
    }

    //compare bone names for all matching tokens and same parent
    void GetConnectedBones(Transform currBone)
    {
        int childCount = currBone.childCount;
        if (childCount == 0)
            return;
        
        Transform[] children = new Transform[currBone.childCount];
        for (int i = 0; i < childCount; i++)
        {
            Transform child = currBone.GetChild(i);
            if (child != null)
            {
                GetConnectedBones(child);
                children[i] = child;
            }
        }

        for(int i = 0; i < childCount; i++)
        {
            Transform child = children[i];
            if (child == null)
                continue;

            string[] currTokens = GetTokens(child);
            List<int> connectedBoneIndices = new List<int>();

            for (int j = 0; j < childCount; j++)
            {
                if (j == i || children[j] == null)
                    continue;
                Transform otherChild = children[j];
                string[] otherTokens = GetTokens(otherChild);

                if (currTokens.SequenceEqual(otherTokens))
                    connectedBoneIndices.Add(_boneIndexMap[otherChild]);
            }
            _connectedBoneMap.Add(child, connectedBoneIndices);            
        }
    }
    
    //TODO make another option to use octrees
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

    Transform GetClosestBone(Vector3 point)
    {
        float minDistSQ = 99999f;
        Transform closest = null;
        foreach (var trans in _boneList)
        {
            Vector3 bonePos = trans.position;
            Vector3 distance = point - bonePos;
            float distSQ = Vector3.Dot(distance, distance);
            if (distSQ < minDistSQ)
            {
                closest = trans;
                minDistSQ = distSQ;
            }
        }

        return closest;
    }
    
    //find which bones to attach each point to
    void GetPointBones()
    {
        //_boneIndices = new List<int>(_pointsList.Count);
        _pointBoneOffsetList = new List<Vector3>(_pointsList.Count);
        for(int i = 0; i < _pointsList.Count; i++)
        {
            Vector3 point = _pointsList[i];
            Transform closestBone = GetClosestBone(point);
            List<int> connectedBones = _connectedBoneMap[closestBone];
            float[] weights = new float[connectedBones.Count + 1];
            int[] boneIndices = new int[connectedBones.Count + 1];
            boneIndices[0] = _boneIndexMap[closestBone];

            if (connectedBones.Count == 0)
            {
                weights[0] = 1.0f;                
                _pointBoneWeights.Add(new BoneGridCell { position = point - closestBone.position, weights = weights, boneIndices = boneIndices });
            }
            else
            {
                float totalDistance = Vector3.Distance(closestBone.position, point);
                Vector3 weighedPosition = Vector3.zero;
                weights[0] = totalDistance;

                for (int j = 0; j < connectedBones.Count; j++)
                {
                    int index = connectedBones[j];
                    float distance = Vector3.Distance(point, _boneList[index].position);
                    totalDistance += distance;
                    weights[j + 1] = distance;
                    boneIndices[j + 1] = index;
                }
                
                for (int j = 0; j < weights.Length; j++)
                {
                    int boneIndex = boneIndices[j];
                    weights[j] = (totalDistance - weights[j]) / totalDistance;
                    weights[j] /= weights.Length - 1;
                    weighedPosition += _boneList[boneIndex].position * weights[j];
                }

                Vector3 offset = point - weighedPosition;
                _pointBoneWeights.Add(new BoneGridCell { position = offset, weights = weights, boneIndices = boneIndices });
            }
        }
    }

    //basicallly do a raycast in a single direction and see how many times you hit the mesh
    //then do it again back to the cell point to get any back facing meshes
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