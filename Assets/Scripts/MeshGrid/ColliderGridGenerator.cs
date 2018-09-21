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

    public ColliderCell _gridCellObject;
    public Vector3 _gridCellSize;
    private List<ColliderCell> _cellList;

    List<Vector3> _pointsList;
    public List<Vector3> PointList
    {
        get { return _pointsList; }
        private set { _pointsList = value; }
    }

    public int _maxHitCount;
    public bool _multiDirectionalCheck;
    public MeshGridData _meshGridData;

    public bool _writeToFile;
    public string _path;
    public string _fileName;

    private void Awake()
    {
        _cellList = new List<ColliderCell>();
        _pointsList = new List<Vector3>();
    }

    // Use this for initialization
    void Start()
    {
        transform.position = Vector3.zero;
        transform.localScale = Vector3.one;

        GenerateGrid();
        if (_multiDirectionalCheck)
        {
            List<Vector3> checkPointList = new List<Vector3>(_pointsList.Count);
            foreach (Vector3 point in PointList)
            {
                if (IsInsideTest(point, -Vector3.forward))
                    checkPointList.Add(point);
            }
            PointList = checkPointList;
        }
        _meshGridData = new MeshGridData();
        _meshGridData.points = PointList;
        _meshGridData.cellSize = _gridCellSize;

        if (_writeToFile)
        {
            string json = JsonUtility.ToJson(_meshGridData);
            StreamWriter writer = new StreamWriter(_path + _fileName, false);
            writer.WriteLine(json);
            writer.Close();
        }

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

    // Update is called once per frame
    void Update()
    {

    }
}