using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionLogger : MonoBehaviour {
    public GameObject _cubeTest;
    
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        //Debug.Log("position: " + transform.position);
        //Debug.Log("rotation: " + transform.rotation);
        //Debug.Log("scale: " + transform.localScale);
        _cubeTest.transform.position = transform.position;
        //_cubeTest.transform.rotation = transform.rotation;
    }
}
