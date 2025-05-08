using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;

public class ControllerOptimized : MonoBehaviour
{
    [SerializeField] private projectAnimController animController;

    [Header("Setup")]
    public GameObject npcPrefab;
    public GameObject enemyPrefab;
    public int numberOfNPCs = 200;
    public int numberOfEnemies = 200;
    public float moveSpeed = 5f;
    public float stopDistance = 1.0f;
    public float spawnArea = 10f;

    [SerializeField] List<Transform> npcTransformsList = new List<Transform>();
    private List<Transform> enemyTransformsList = new List<Transform>();
    [SerializeField] List<NPCContext> npcContexts = new List<NPCContext>();
    private List<NPCContext> enemyContexts = new List<NPCContext>();
    private TransformAccessArray npcsToMove;
    private TransformAccessArray enemiesToMove;
    private JobHandle combinedMovementHandle;
    private JobHandle moveNpcHandle;
    private JobHandle moveEnemyHandle;

    // NativeArrays for Position Snapshots (Jobs READ from these)
    private NativeArray<float3> npcPositionsSnapshot;
    private NativeArray<float3> enemyPositionsSnapshot;

    [Unity.Burst.BurstCompile]
    struct MoveTowardsTargetJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> targetPositions;
        [ReadOnly] public float moveSpeed;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float stopDistanceSq;

        public void Execute(int index, TransformAccess transform)
        {
            if (targetPositions.Length == 0) return;

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

            if (minDistanceSq > stopDistanceSq)
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
            var context = go.GetComponent<NPCContext>();
            context.TeamID = 0;
            npcContexts.Add(context);
        }

        for (int i = 0; i < numberOfEnemies; i++)
        {
            GameObject go = Instantiate(enemyPrefab, GetRandomSpawnPosition(), Quaternion.identity);
            enemyTransformsList.Add(go.transform);
            var context = go.GetComponent<NPCContext>();
            context.TeamID = 1;
            enemyContexts.Add(context);
        }

        npcsToMove = new TransformAccessArray(npcTransformsList.ToArray());
        enemiesToMove = new TransformAccessArray(enemyTransformsList.ToArray());

        npcPositionsSnapshot = new NativeArray<float3>(numberOfNPCs, Allocator.Persistent);
        enemyPositionsSnapshot = new NativeArray<float3>(numberOfEnemies, Allocator.Persistent);

        animController.AssignNPCS();
    }

    void Update()
    {
        if (!enemiesToMove.isCreated || !npcsToMove.isCreated || npcPositionsSnapshot.Length == 0 || enemyPositionsSnapshot.Length == 0)
        {
            return;
        }

        JobHandle gatherNpcHandle = default;
        JobHandle gatherEnemyHandle = default;

        // Gather position snapshots for NPCs and enemies
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
                targetPositions = enemyPositionsSnapshot,
                moveSpeed = moveSpeed,
                deltaTime = Time.deltaTime,
                stopDistanceSq = stopDistance * stopDistance
            };
            moveNpcHandle = moveNpc.Schedule(npcsToMove, combinedGatherHandle);
        }

        if (enemiesToMove.length > 0 && npcPositionsSnapshot.Length > 0)
        {
            MoveTowardsTargetJob moveEnemy = new MoveTowardsTargetJob
            {
                targetPositions = npcPositionsSnapshot,
                moveSpeed = moveSpeed,
                deltaTime = Time.deltaTime,
                stopDistanceSq = stopDistance * stopDistance
            };
            moveEnemyHandle = moveEnemy.Schedule(enemiesToMove, combinedGatherHandle);
        }

        combinedMovementHandle = JobHandle.CombineDependencies(moveNpcHandle, moveEnemyHandle);
        JobHandle.ScheduleBatchedJobs();
    }

    void LateUpdate()
    {
        if (!npcsToMove.isCreated && !enemiesToMove.isCreated)
        {
            return;
        }

        // Complete the job and ensure it's finished before updating positions
        combinedMovementHandle.Complete();

        UpdateNPCStates();
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spawnArea;
        return new Vector3(randomCircle.x, 1.46f, randomCircle.y);
    }

    void UpdateNPCStates()
    {
        // Update NPC states based on closest targets
        foreach (var npc in npcContexts)
        {
            if (npc == null) continue;
            Transform closestEnemy = FindClosestEnemy(npc.transform, enemyContexts);
            npc.Target = closestEnemy;
            npc.UpdateStateBasedOnTarget();
        }

        // Same for enemies
        foreach (var enemy in enemyContexts)
        {
            if (enemy == null) continue;
            Transform closestNpc = FindClosestEnemy(enemy.transform, npcContexts);
            enemy.Target = closestNpc;
            enemy.UpdateStateBasedOnTarget();
        }
    }

    Transform FindClosestEnemy(Transform source, List<NPCContext> potentialTargets)
    {
        float minDist = float.MaxValue;
        Transform closest = null;

        foreach (var target in potentialTargets)
        {
            if (target == null) continue;

            float dist = Vector3.Distance(source.position, target.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = target.transform;
            }
        }

        return closest;
    }

    public void RemoveNPC(NPCContext npc)
    {
        combinedMovementHandle.Complete(); // Ensure jobs are complete before modifying lists

        if (npc.TeamID == 0)
        {
            int index = npcContexts.IndexOf(npc);
            if (index != -1)
            {
                npcContexts.RemoveAt(index);
                npcTransformsList.RemoveAt(index);
                numberOfNPCs -= 1;
            }
        }
        else
        {
            int index = enemyContexts.IndexOf(npc);
            if (index != -1)
            {
                enemyContexts.RemoveAt(index);
                enemyTransformsList.RemoveAt(index);
                numberOfEnemies -= 1;
            }
        }

        RebuildTransformAccessArrays();

        if (npcPositionsSnapshot.IsCreated) npcPositionsSnapshot.Dispose();
        if (enemyPositionsSnapshot.IsCreated) enemyPositionsSnapshot.Dispose();
        npcPositionsSnapshot = new NativeArray<float3>(numberOfNPCs, Allocator.Persistent);
        enemyPositionsSnapshot = new NativeArray<float3>(numberOfEnemies, Allocator.Persistent);
    }

    void RebuildTransformAccessArrays()
    {
        if (npcsToMove.isCreated) npcsToMove.Dispose();
        if (enemiesToMove.isCreated) enemiesToMove.Dispose();

        npcsToMove = new TransformAccessArray(npcTransformsList.ToArray());
        enemiesToMove = new TransformAccessArray(enemyTransformsList.ToArray());
    }

    void OnDestroy()
    {
        if (npcsToMove.isCreated || enemiesToMove.isCreated || npcPositionsSnapshot.IsCreated || enemyPositionsSnapshot.IsCreated)
        {
            combinedMovementHandle.Complete();
        }

        if (npcsToMove.isCreated) npcsToMove.Dispose();
        if (enemiesToMove.isCreated) enemiesToMove.Dispose();
        if (npcPositionsSnapshot.IsCreated) npcPositionsSnapshot.Dispose();
        if (enemyPositionsSnapshot.IsCreated) enemyPositionsSnapshot.Dispose();
    }
}
