using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

public class OctreeSystem : JobComponentSystem
{
    [Inject] SwarmData _swarmData;

    public NativeMultiHashMap<uint, OctreeNode> _octreeMap;
    
    struct SwarmData
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Pikmin> pikmin;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public ComponentDataArray<SwarmFlockFormation> swarm;
    }

    public struct OctreeNode
    {
        public NativeArray<int> entityIDs;
        public int entCount; //entities in octant including children
        public uint locCode;
        public byte childFlag;
        public float3 min;
        public float size;
    }

    struct UpdateOctreeJob : IJob
    {
        [ReadOnly] public int Length;
        [ReadOnly] public ComponentDataArray<Velocity> velocity;
        [ReadOnly] public ComponentDataArray<Position> position;

        public NativeMultiHashMap<uint, OctreeNode> octreeMap;
        //NativeArray<int> attached;

        [ReadOnly] public int MAX_DEPTH;
        [ReadOnly] public int MAX_ENTITES;
        [ReadOnly] public int MIN_DEPTH;        

        public bool LookUpNode(uint code, out OctreeNode output)
        {
            NativeMultiHashMapIterator<uint> it;
            return octreeMap.TryGetFirstValue(code, out output, out it);
        }

        public uint GenLocCode(uint currCode, OctreeNode node, float3 position, out byte child)
        {
            float childSize = node.size * .5f;
            int x = position.x >= node.min.x && position.x < node.min.x + childSize ? 0 : 1;
            int y = position.x >= node.min.x && position.x < node.min.x + childSize ? 0 : 1;
            int z = position.x >= node.min.x && position.x < node.min.x + childSize ? 0 : 1;

            int index = 4 * x + 2 * y + z;
            child = (byte)index;

            return (uint)((currCode << 3) | index);
        }

        public void Insert(int index, float3 position)
        {
            int i = 0;
            int currCount = 0;
            uint locCode = 1;
            float size;
            float3 childPos;
            
            NativeArray<int> octantEnts;
            OctreeNode curr;
            OctreeNode next;
            LookUpNode(locCode, out curr);

            while (i++ < MAX_DEPTH)
            {
                byte childFlag = 0;

                curr.entCount = curr.entCount + 1;
                locCode = GenLocCode(locCode, curr, position, out childFlag);
                if(!LookUpNode(locCode, out next))
                {
                    curr.childFlag |= childFlag;
                    next = new OctreeNode { entCount = 1, locCode = locCode, childFlag = childFlag, size = curr.size * .5f, };
                    octreeMap.Add(locCode, next);
                }
                else
                {
                    curr.childFlag |= childFlag;
                }
                curr = next; 
            }

            if (!curr.entityIDs.IsCreated)
                curr.entityIDs = new NativeArray<int>(MAX_ENTITES, Allocator.Persistent);

            int entities = curr.entCount;                
            curr.entityIDs[curr.entCount] = index;
            curr.entCount = curr.entCount + 1;
        }


        public void Execute()
        {
            for(int i = 0; i < Length; i++)
            {
                Insert(i, position[i].Value);
            }
        }

    }

    protected override void OnStartRunning()
    {
        _octreeMap = new NativeMultiHashMap<uint, OctreeNode>(100, Allocator.Persistent);
        base.OnStartRunning();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        return new UpdateOctreeJob
        {
            Length = _swarmData.Length,
            MAX_ENTITES = 20,
            MIN_DEPTH = 9,
            MAX_DEPTH = 9,
            octreeMap = _octreeMap,
            velocity = _swarmData.velocity,
            position = _swarmData.position

        }.Schedule(inputDeps);
    }
}
