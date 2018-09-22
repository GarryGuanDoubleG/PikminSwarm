using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinnedMeshCollider : MonoBehaviour {

    public SkinnedMeshRenderer meshRenderer;
    private MeshCollider collider;
    private Mesh colliderMesh;
    private void Awake()
    {
       var scale = new Vector3(1.0f / transform.localScale.x, 1.0f / transform.localScale.y, 1.0f / transform.localScale.z);

        collider = GetComponent<MeshCollider>();
        colliderMesh = new Mesh();
        meshRenderer.BakeMesh(colliderMesh);

        Vector3[] vertices = new Vector3[colliderMesh.vertices.Length];
        for (int i = 0; i < colliderMesh.vertices.Length; i++)
            vertices[i] = Vector3.Scale(scale, colliderMesh.vertices[i]);
        colliderMesh.vertices = vertices;
        collider.sharedMesh = null;
        collider.sharedMesh = colliderMesh;
    }
    // Use this for initialization
    void Start () {

    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
