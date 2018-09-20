using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class ColliderGrid : MonoBehaviour {

    public MeshCollider meshCollider;
    public BoxCollider meshBoxCollider;
    public GameObject rootObject;

    public ColliderCell gridCellObject;
    public Vector3 gridCellSize;

    private List<ColliderCell> cellList;

    List<Vector3> pointsList;
    public List<Vector3> PointList
    {
        get { return pointsList; }
    }

    public TextAsset bakedPoints;
    public MeshGrid meshGrid;
    public bool generateGrid;
    public bool spawnColliderObj;
    public string path;
    public string fileName;
    
    private void Awake()
    {
        cellList = new List<ColliderCell>();
        pointsList = new List<Vector3>();
    }

    // Use this for initialization
    void Start() {

       if(bakedPoints == null || generateGrid)
        {
            transform.position = Vector3.zero;
            transform.localScale = Vector3.one;

            GenerateGrid();
            meshGrid = new MeshGrid();
            meshGrid.points = PointList;
            meshGrid.cellSize = gridCellSize;

            string json = JsonUtility.ToJson(meshGrid);
            StreamWriter writer = new StreamWriter(path + fileName, false);
            writer.WriteLine(json);
            writer.Close();
        }
       else
        {
            meshGrid = JsonUtility.FromJson<MeshGrid>(bakedPoints.text);
            gridCellSize = meshGrid.cellSize;
            pointsList = meshGrid.points;            
        }

        if (spawnColliderObj)
        {
            GameObject[] gameObjArr = new GameObject[PointList.Count];
            int count = 0;
            foreach (var point in PointList)
            {
                GameObject cell = Instantiate(gridCellObject.gameObject, rootObject.transform);
                gameObjArr[count] = cell;
                cell.transform.localPosition = point;

                ColliderCell cellCollider = cell.GetComponent<ColliderCell>();
                cellCollider.cellCollider.size = gridCellSize;
                cellList.Add(cell.GetComponent<ColliderCell>());
                count++;
            }
        }        
    }

    void GenerateGrid()
    {
        Vector3 boxColliderSize = meshBoxCollider.size;
        Vector3 worldSize = Vector3.Scale(meshBoxCollider.size, meshBoxCollider.transform.localScale) + Vector3.one;
        Vector3 offset = worldSize * .5f;
        offset.y = 0.0f;

        Vector3 cellsPerSide = new Vector3(worldSize.x / gridCellSize.x, worldSize.y / gridCellSize.y, worldSize.z / gridCellSize.z);
        Vector3 halfCellSize = gridCellSize * .5f;

        int colliderCount = (int)(cellsPerSide.x * cellsPerSide.y * cellsPerSide.z) + 1;

        int count = 0;
        GameObject[] gameObjArr = new GameObject[colliderCount];

        for (int x = 0; x <= (int)cellsPerSide.x; x++)
        {
            for (int y = 0; y <= (int)cellsPerSide.y; y++)
            {
                for (int z = 0; z <= (int)cellsPerSide.z; z++)
                {
                    Vector3 pos = halfCellSize + (new Vector3(x * gridCellSize.x, y * gridCellSize.y, z * gridCellSize.z));
                    pos -= offset;
                    if (IsInsideTest(pos))
                    {
                        GameObject cell = Instantiate(gridCellObject.gameObject, rootObject.transform);
                        gameObjArr[count] = cell;
                        cell.transform.localPosition = pos;

                        ColliderCell cellCollider = cell.GetComponent<ColliderCell>();
                        cellCollider.cellCollider.size = gridCellSize;
                        cellList.Add(cell.GetComponent<ColliderCell>());
                        PointList.Add(pos);
                        count++;
                    }
                }
            }
        }
    }

    public bool IsInsideTest(Vector3 cellPoint)
    {
        int hitCount = 0;
        float maxDistance = 500.0f;
        Vector3 goal = cellPoint + Vector3.up * maxDistance;
        Vector3 currPoint;
        currPoint = cellPoint;
        Vector3 dir = Vector3.Normalize(goal - currPoint);
        float hitPointOffset = 0.001f;

        //basicallly do a raycast in a single direction and see how many times you hit the mesh
        //then do it again back to the cell point to get any back facing meshes
        while (currPoint != goal)
        {
            RaycastHit hit;
            if(Physics.Linecast(currPoint, goal, out hit) && hit.collider != meshBoxCollider)
            {
                hitCount++;
                currPoint = hit.point + dir * hitPointOffset;
            }
            else
                currPoint = goal;
        }
        while (currPoint != cellPoint)
        {
            RaycastHit hit;
            if (Physics.Linecast(currPoint, cellPoint, out hit) && hit.collider != meshBoxCollider)
            {
                hitCount++;
                currPoint = hit.point + -dir * hitPointOffset;
            }
            else
                currPoint = cellPoint;
        }

        return !(hitCount % 2 == 0);

    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
