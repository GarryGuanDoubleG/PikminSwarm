using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;

public class SpawnSystem : ComponentSystem
{
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        Enabled = false;

        int count = Bootstrap.settings._spawnCount;

        //spawn in a circle        
        EntityManager entityManager = EntityManager;
        var pikmin = entityManager.CreateEntity(Bootstrap.pikminArch);
        var pikminLook = Bootstrap.settings._pikminLook;
        var pikminArr = new NativeArray<Entity>(count, Allocator.Temp);
        entityManager.Instantiate(pikmin, pikminArr);

        var rand = new Random(0x4564ff);        
        float offset = 360.0f / count;
        for (int i = 0; i < count; i++)
        {
            float distance = rand.NextFloat(10.0f, 50.0f);
            float rad = math.radians(i * (offset + rand.NextFloat(0.0f, 20.0f)));
            entityManager.SetComponentData(pikminArr[i], new Position { Value = new float3(math.cos(rad), rand.NextFloat(-1.0f, 1.0f), math.sin(rad)) * distance});
            entityManager.SetComponentData(pikminArr[i], new Velocity { Value = float3.zero });
            entityManager.SetComponentData(pikminArr[i], new Scale { Value = new float3(.3f) });
            entityManager.AddSharedComponentData(pikminArr[i], pikminLook[(i % Bootstrap.settings._pikminLook.Count)].Value);
        }
        entityManager.DestroyEntity(pikmin);
        pikminArr.Dispose();
    }

    protected override void OnUpdate()
    {

    }
}
