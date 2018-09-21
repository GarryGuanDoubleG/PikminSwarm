using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderCell : MonoBehaviour {

    public BoxCollider cellCollider;
    public bool hitMesh;
    private float timer;

    private void Start()
    {
        timer = 123.0f;
    }
    public void OnCollisionEnter(Collision collision)
    {
        hitMesh = true;        
    }

    public void OnTriggerEnter(Collider other)
    {
        hitMesh = true;

        Debug.Log("HIT");
    }

    public void OnCollisionStay(Collision collision)
    {
        Debug.Log("Collision");
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0.0 && !hitMesh)
            Destroy(gameObject);
    }

}
