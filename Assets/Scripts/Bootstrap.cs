using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Rendering;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

public class Bootstrap
{
    public static Settings settings;

    public static EntityArchetype playerArch;
    public static EntityArchetype pikminArch;    

    public static MeshInstanceRenderer playerLook;
    public static MeshInstanceRenderer pikminLook;    

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Init()
    {
        settings = GameObject.FindObjectOfType<Settings>();

        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        playerArch = entityManager.CreateArchetype(
            typeof(Player),
            typeof(Position),
            typeof(Rotation));

        pikminArch = entityManager.CreateArchetype(
            typeof(Pikmin),
            typeof(Velocity),
            typeof(Position));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeWithScene()
    {
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        playerLook = GetLookFromPrototype("playerLook");
        pikminLook = GetLookFromPrototype("pikminLook");

        Entity player = entityManager.CreateEntity(playerArch);
        entityManager.SetComponentData(player, new Rotation { });
        entityManager.SetComponentData(player, new Position { Value = new float3(0, 1.0f, 0) });
        entityManager.AddSharedComponentData(player, playerLook);
    }

    public static MeshInstanceRenderer GetLookFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<MeshInstanceRendererComponent>().Value;

        if (result.mesh == null || result.material == null)
            Debug.Log("Mesh and Material for " + protoName + " must be set");

        Object.Destroy(proto);
        return result;
    }
}
