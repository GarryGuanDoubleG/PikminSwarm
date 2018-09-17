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
        var pikminLook = Bootstrap.pikminLook;
        var pikminArr = new NativeArray<Entity>(count, Allocator.Temp);
        entityManager.Instantiate(pikmin, pikminArr);

        var rand = new Random(0x4564ff);
        float distance = 20.0f;
        float offset = 360.0f / count;
        for (int i = 0; i < count; i++)
        {
            float rad = math.radians(i * (offset + rand.NextFloat(0.0f, 20.0f)));
            entityManager.SetComponentData(pikminArr[i], new Position { Value = new float3(math.cos(rad), rand.NextFloat(-1.0f, 1.0f), math.sin(rad)) * distance});
            entityManager.SetComponentData(pikminArr[i], new Velocity { Value = float3.zero });
            entityManager.AddSharedComponentData(pikminArr[i], pikminLook);
        }
        entityManager.DestroyEntity(pikmin);
        pikminArr.Dispose();
    }

    protected override void OnUpdate()
    {

    }
}
