using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class SwarmModelComponent : MonoBehaviour {

    public float timer;
    public GameObjectEntity entity;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        timer -= Time.deltaTime;
        if(timer <= 0)
        {
            EntityManager entityManager = World.Active.GetOrCreateManager<EntityManager>();
            entityManager.AddComponent(entity.Entity, typeof(TargetComponent));

            Destroy(this);
        }
	}
}
