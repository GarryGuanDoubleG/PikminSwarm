using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinnedMeshCollider : MonoBehaviour {

    public SkinnedMeshRenderer meshRenderer;
    public MeshCollider meshCollider;
    private Mesh colliderMesh;

    public void RecalculateMeshCollider()
    {
        Vector3 skinMeshScale = meshRenderer.transform.localScale;
        var scale = new Vector3(1.0f / skinMeshScale.x, 1.0f / skinMeshScale.y, 1.0f / skinMeshScale.z);

        colliderMesh = new Mesh();
        meshRenderer.BakeMesh(colliderMesh);
        Vector3[] vertices = new Vector3[colliderMesh.vertices.Length];
        for (int i = 0; i < colliderMesh.vertices.Length; i++)
            vertices[i] = Vector3.Scale(scale, colliderMesh.vertices[i]);

        colliderMesh.vertices = vertices;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = colliderMesh;
    }
}
