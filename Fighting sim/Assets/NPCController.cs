using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;


public class NPCController : MonoBehaviour
{
    [Header("Setup")]
    public GameObject npcPrefab;
    public GameObject enemyPrefab;
    public int numberOfNPCs = 3; 
    public int numberOfEnemies = 3;
    public float moveSpeed = 5f;
    public float stopDistance = 1.0f;
    public float spawnArea = 10f; 

    private List<Transform> npcTransformsList = new List<Transform>();
    private List<Transform> enemyTransformsList = new List<Transform>();
    private TransformAccessArray npcsToMove;
    private TransformAccessArray enemiesToMove;
    private JobHandle combinedMovementHandle;
    private JobHandle moveNpcHandle;
    private JobHandle moveEnemyHandle;
    // NativeArrays for Position Snapshots (Jobs READ from these)
    private NativeArray<float3> npcPositionsSnapshot;
    private NativeArray<float3> enemyPositionsSnapshot;

    [Unity.Burst.BurstCompile] //this is for efficiency. It basically transforms the code to more efficient machine code. It requires the math module tho.
    struct MoveTowardsTargetJob : IJobParallelForTransform
    {

        [ReadOnly] public NativeArray<float3> targetPositions;
        [ReadOnly]public float moveSpeed;
        [ReadOnly]public float deltaTime; // Time.deltaTime from the main thread
        [ReadOnly] public float stopDistanceSq;

        // Execute is called for each item in the TransformAccessArray
        // The index tells us which transform we are currently processing
        // transform provides safe access to the transform component from the job
        public void Execute(int index, TransformAccess transform)
        {
            if (targetPositions.Length == 0)
            {
                return;
            }
            float3 currentPosition = transform.position;
            float3 closestTargetPosition = targetPositions[0];
            float minDistanceSq = math.distancesq(currentPosition, closestTargetPosition);
            for (int i = 1; i < targetPositions.Length; i++)
            {
                float3 targetPos = targetPositions[i];
                float distSq = math.distancesq(currentPosition, targetPos);
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closestTargetPosition = targetPos;
                }
            }
            if (minDistanceSq > stopDistanceSq) //we take squared stopDistance here
            {
                float3 directionToTarget = math.normalize(closestTargetPosition - currentPosition);
                float3 movementStep = directionToTarget * moveSpeed * deltaTime;

                transform.position = currentPosition + movementStep;

                if (math.lengthsq(directionToTarget) > 0.001f) 
                {
                    transform.rotation = quaternion.LookRotationSafe(directionToTarget, math.up());
                }
            }
        }
    }

    [Unity.Burst.BurstCompile]
    struct GatherPositionsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<float3> PositionsSnapshot; 
        public void Execute(int index, TransformAccess transform)
        {
            if (index < PositionsSnapshot.Length)
            {
                PositionsSnapshot[index] = transform.position;
            }
        }
    }

    
    void Start()
    {
        for (int i = 0; i < numberOfNPCs; i++)
        {
            GameObject go = Instantiate(npcPrefab, GetRandomSpawnPosition(), Quaternion.identity);
            npcTransformsList.Add(go.transform);
        }

        for (int i=0; i < numberOfEnemies; i++)
        {
            GameObject go = Instantiate(enemyPrefab, GetRandomSpawnPosition(), Quaternion.identity);
            enemyTransformsList.Add(go.transform);
        }

        if (npcTransformsList.Count > 0)
            npcsToMove = new TransformAccessArray(npcTransformsList.ToArray());
        else //in case no npcs
            npcsToMove = new TransformAccessArray(0);


        if (enemyTransformsList.Count > 0)
            enemiesToMove = new TransformAccessArray(enemyTransformsList.ToArray());
        else 
            enemiesToMove = new TransformAccessArray(0);

        npcPositionsSnapshot = new NativeArray<float3>(numberOfNPCs, Allocator.Persistent);
        enemyPositionsSnapshot = new NativeArray<float3>(numberOfEnemies, Allocator.Persistent);

    }

    void Update()
    {

        if (!enemiesToMove.isCreated || !npcsToMove.isCreated || npcPositionsSnapshot.Length == 0 || enemyPositionsSnapshot.Length == 0)
        {
            return;
        }

        JobHandle gatherNpcHandle = default;
        JobHandle gatherEnemyHandle = default;

        //first we gather position snapshots
        if (npcsToMove.length > 0 && npcPositionsSnapshot.IsCreated) 
        {
            GatherPositionsJob gatherNpcPosJob = new GatherPositionsJob { PositionsSnapshot = npcPositionsSnapshot };
            gatherNpcHandle = gatherNpcPosJob.Schedule(npcsToMove);
        }

        if (enemiesToMove.length > 0 && enemyPositionsSnapshot.IsCreated) 
        {
            GatherPositionsJob gatherEnemyPosJob = new GatherPositionsJob { PositionsSnapshot = enemyPositionsSnapshot };
            gatherEnemyHandle = gatherEnemyPosJob.Schedule(enemiesToMove);
        }

        JobHandle combinedGatherHandle = JobHandle.CombineDependencies(gatherNpcHandle, gatherEnemyHandle);



        if (npcsToMove.length > 0 && enemyPositionsSnapshot.Length > 0)
        {
            MoveTowardsTargetJob moveNpc = new MoveTowardsTargetJob
            {
                targetPositions = enemyPositionsSnapshot, // Get target position on main thread
                moveSpeed = moveSpeed,
                deltaTime = Time.deltaTime, // Get delta time on main thread
                stopDistanceSq = stopDistance * stopDistance 
            };
            moveNpcHandle = moveNpc.Schedule(npcsToMove, combinedGatherHandle);
        }


        if (enemiesToMove.length > 0 && npcPositionsSnapshot.Length > 0)
        {
            MoveTowardsTargetJob moveEnemy = new MoveTowardsTargetJob
            {
                targetPositions = npcPositionsSnapshot, // Get target position on main thread
                moveSpeed = moveSpeed,
                deltaTime = Time.deltaTime, // Get delta time on main thread
                stopDistanceSq = stopDistance * stopDistance
            };
            moveEnemyHandle = moveEnemy.Schedule(enemiesToMove, combinedGatherHandle);

        }

        combinedMovementHandle = JobHandle.CombineDependencies(moveNpcHandle, moveEnemyHandle);

        JobHandle.ScheduleBatchedJobs(); //this is to ensure that jobs are scheduled right away

    }

    void LateUpdate()
    {

        if (!npcsToMove.isCreated && !enemiesToMove.isCreated)
        {
            return;
        }

        // --- 3. Complete the Job (Main Thread) ---
        // Calling Complete() ensures the main thread waits for the job to finish.
        // It's crucial to complete jobs before you need their results or
        // before disposing of the Native Collections/TransformAccessArray the job is using.
        // LateUpdate is a good place for movement jobs because it's after Update
        // where input might happen, and before rendering.
        
        combinedMovementHandle.Complete();//only last handle needs to be complete in dependency chain

        // Now that the job is complete, the transforms' positions have been updated.
    }

    // Helper to get a random position for spawning
    private Vector3 GetRandomSpawnPosition() 
    {
        // Simple example: random point on a circle/sphere
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spawnArea;
        return new Vector3(randomCircle.x, -0.19f, randomCircle.y);
    }

    // --- Clean up Native Collections (VERY IMPORTANT!) ---
    // Native Collections (like TransformAccessArray) live in unmanaged memory.
    // They are not garbage collected automatically and MUST be manually Disposed.
    // OnDestroy is a good place to do this when the object managing them is destroyed.
    void OnDestroy()
    {
        if (npcsToMove.isCreated || enemiesToMove.isCreated || npcPositionsSnapshot.IsCreated || enemyPositionsSnapshot.IsCreated)
        {
            moveEnemyHandle.Complete(); // Completes the entire chain
        }



        if (npcsToMove.isCreated)
        {
            npcsToMove.Dispose();

        }

        if (enemiesToMove.isCreated)
        {
            enemiesToMove.Dispose();
        }

        if (npcPositionsSnapshot.IsCreated)
        {
            npcPositionsSnapshot.Dispose();
        }

        if (enemyPositionsSnapshot.IsCreated)
        {
            enemyPositionsSnapshot.Dispose();
        }
    }
}