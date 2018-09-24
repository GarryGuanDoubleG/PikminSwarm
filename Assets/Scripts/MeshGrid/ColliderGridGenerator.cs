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
    public SkinnedMeshRenderer _skinnedMeshRenderer;
    public BoxCollider[] _meshBoxColliderArr;

    public GameObject _rootObject;
    public GameObject _rootMesh;
    public GameObject _rootArmature;

    private List<Transform> _boneList;
    private Dictionary<Transform, int> _boneIndexMap;
    private Dictionary<Transform, List<int>> _connectedBoneMap; //bones with the same parent that affect bone position
    //private List<int> _boneIndices;
    private List<int> _boneWeightIndices;

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
        _boneWeightIndices = new List<int>();

        if (_isSkinnedMesh)
        {
            GetBonesList(_rootArmature.transform);
            //GetConnectedBones(_rootArmature.transform);
        }

        if (_generateGrid)
        {
            GenerateGrid();

            if (_isSkinnedMesh) FindBoneWeightsFromPoint();
            if (_writeToFile) WriteToFile();
            if(_showPoints) SpawnColliders();
        }
    }

    void WriteToFile()
    {
        _meshGridData = new MeshGridData();
        _meshGridData.isRigged = _isSkinnedMesh;
        _meshGridData.cellSize = _gridCellSize;
        _meshGridData.points = _pointsList;
        _meshGridData.boneWeightIndices = _isSkinnedMesh ? _boneWeightIndices : null;

        string json = JsonUtility.ToJson(_meshGridData);
        StreamWriter writer = new StreamWriter(_path + _fileName, false);
        writer.WriteLine(json);
        writer.Close();
    }

    void GetBonesList(Transform transform)
    {
        int i = 0;
        Transform[] bones = _skinnedMeshRenderer.bones;        
        foreach (Transform bone in bones)
        {
            _boneList.Add(bone);
            _boneIndexMap.Add(bone, i++);
        }

        //int childCount = transform.childCount;
        //for(int i = 0; i < childCount; i++)
        //{
        //    Transform child = transform.GetChild(i);
        //    if (child != null)
        //    {
        //        _boneIndexMap.Add(child, _boneList.Count);
        //        _boneList.Add(child);
        //        GetBonesList(child);
        //    }
        //}
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
        foreach(Transform bone in _boneList)
        {
            string[] tokens = GetTokens(bone);
            List<int> indices = new List<int>();
            Transform parent = bone.parent;
            Vector3 pos = bone.position;

            float minDist1 = 9999,
                    minDist2 = 9999,
                    minDist3 = 9999;
            Transform bone1 = null,
                      bone2 = null,
                      bone3 = null;
            
            foreach(Transform other in _boneList)
            {
                if (other != bone)
                {
                    float dist = Vector3.Distance(other.position, pos);
                    if(dist < minDist1)
                    {
                        bone3 = bone2;
                        bone2 = bone1;
                        bone1 = other;    
                        
                        minDist3 = minDist2;
                        minDist2 = minDist1;
                        minDist1 = dist;
                    }
                    else if(dist < minDist2)
                    {
                        bone3 = bone2;
                        bone2 = other;
                        minDist3 = minDist2;
                        minDist2 = dist;
                    }
                    else if(dist < minDist3)
                    {
                        bone3 = other;
                        minDist3 = dist;
                    }
                }
            }
            indices.Add(_boneIndexMap[bone1]);
            indices.Add(_boneIndexMap[bone2]);
            indices.Add(_boneIndexMap[bone3]);

            _connectedBoneMap.Add(bone, indices);
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
    void FindBoneWeightsFromPoint()
    {
        Mesh mesh = _skinnedMeshRenderer.sharedMesh;
        Vector3 [] vertices = mesh.vertices;
        BoneWeight[] boneWeights = mesh.boneWeights;
        Vector3 scale = _skinnedMeshRenderer.transform.localScale;
        for (int i = 0; i < _pointsList.Count; i++)
        {
            Vector3 point = _pointsList[i];
            float minDistSQ = 9999f;
            int index = -1;
            for(int j = 0; j < vertices.Length; j++)
            {
                Vector3 vertex = Vector3.Scale(scale, vertices[j]);
                float distSQ = Vector3.SqrMagnitude(vertex - point);
                if (distSQ < minDistSQ)
                {
                    index = j;
                    minDistSQ = distSQ;
                }
            }
            Vector3 weighedPoint = Vector3.zero;
            BoneWeight bW = boneWeights[index];
            weighedPoint += bW.weight0 * _boneList[bW.boneIndex0].position;
            weighedPoint += bW.weight1 * _boneList[bW.boneIndex1].position;
            weighedPoint += bW.weight2 * _boneList[bW.boneIndex2].position;
            weighedPoint += bW.weight3 * _boneList[bW.boneIndex3].position;

            _pointsList[i] = point - weighedPoint;
            _boneWeightIndices.Add(index);
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