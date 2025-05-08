using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.Jobs;
using Unity.Mathematics;

public class NPCsController : MonoBehaviour
{
    [System.Serializable]
    public struct Team
    {
        public GameObject prefab;
        public int count;
    }

    [Header("Setup")]
    public float moveSpeed = 5f;
    public float stopDistance = 1.0f;
    public float attackRange = 1.5f;
    public float spawnArea = 10f;
    public Team[] teams;

    private List<TeamData> teamDataList = new List<TeamData>();
    private List<JobHandle> movementHandles = new List<JobHandle>();
    private List<JobHandle> detectionHandles = new List<JobHandle>();
    private List<JobHandle> attackHandles = new List<JobHandle>();
    private JobHandle movementHandle;
    private JobHandle detectionHandle;
    private JobHandle attackHandle;
    private JobHandle combinedHandle;

    private bool teamsCreated = false;

    struct TeamData
    {
        public int teamID;
        public TransformAccessArray transforms;
        public NativeArray<float3> positions;
    }

    [BurstCompile]
    struct GatherPositionsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<float3> positions;

        public void Execute(int index, TransformAccess transform)
        {
            positions[index] = transform.position;
        }
    }

    [BurstCompile]
    struct MoveTowardsClosestJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> enemyPositions;
        [ReadOnly] public float moveSpeed;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float stopDistanceSq;

        public void Execute(int index, TransformAccess transform)
        {
            float3 current = transform.position;
            if (enemyPositions.Length == 0)
                return;

            float3 closest = enemyPositions[0];
            float minDist = math.distancesq(current, closest);
            for (int i = 1; i < enemyPositions.Length; i++)
            {
                float dist = math.distancesq(current, enemyPositions[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = enemyPositions[i];
                }
            }

            if (minDist > stopDistanceSq)
            {
                float3 dir = math.normalize(closest - current);
                transform.position = current + dir * moveSpeed * deltaTime;
                if (math.lengthsq(dir) > 0.001f)
                    transform.rotation = quaternion.LookRotationSafe(dir, math.up());
            }
        }
    }

    [BurstCompile]
    struct CheckNearbyEnemyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> selfPositions;
        [ReadOnly] public NativeArray<float3> enemyPositions;
        [ReadOnly] public float attackRangeSq;
        public NativeArray<bool> canAttack;

        public void Execute(int index)
        {
            float3 selfPos = selfPositions[index];
            for (int i = 0; i < enemyPositions.Length; i++)
            {
                if (math.distancesq(selfPos, enemyPositions[i]) <= attackRangeSq)
                {
                    canAttack[index] = true;
                    return;
                }
            }
            canAttack[index] = false;
        }
    }

    [BurstCompile]
    struct AttackEnemyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<bool> canAttack;

        public void Execute(int index)
        {
            if (canAttack[index])
            {
                // Add attack logic here
                // This is just a placeholder
                // Debug.Log("Attacking from index " + index);
            }
        }
    }

    void Start()
    {
        for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
        {
            var team = teams[teamIndex];
            List<Transform> transforms = new List<Transform>();
            for (int i = 0; i < team.count; i++)
            {
                GameObject obj = Instantiate(team.prefab, GetRandomSpawnPosition(), Quaternion.identity);
                transforms.Add(obj.transform);
            }

            TeamData data = new TeamData
            {
                teamID = teamIndex,
                transforms = new TransformAccessArray(transforms.ToArray()),
                positions = new NativeArray<float3>(transforms.Count, Allocator.Persistent)
            };

            teamDataList.Add(data);
        }

        teamsCreated = true;
    }

    void Update()
    {
        movementHandles.Clear();
        detectionHandles.Clear();
        attackHandles.Clear();

        var gatherHandles = new NativeArray<JobHandle>(teamDataList.Count, Allocator.Temp);
        for (int i = 0; i < teamDataList.Count; i++)
        {
            var team = teamDataList[i];
            var gather = new GatherPositionsJob { positions = team.positions };
            gatherHandles[i] = gather.Schedule(team.transforms);
        }

        // Complete all gather jobs before proceeding
        JobHandle.CompleteAll(gatherHandles);
        gatherHandles.Dispose();



        for (int i = 0; i < teamDataList.Count; i++)
        {
            var team = teamDataList[i];

            for (int j = 0; j < teamDataList.Count; j++)
            {
                if (i == j) continue;
                var enemyTeam = teamDataList[j];

                var moveJob = new MoveTowardsClosestJob
                {
                    enemyPositions = enemyTeam.positions,
                    moveSpeed = moveSpeed,
                    deltaTime = Time.deltaTime,
                    stopDistanceSq = stopDistance * stopDistance
                };
                movementHandle = moveJob.Schedule(team.transforms, default);

                var attackCheckFlags = new NativeArray<bool>(team.transforms.length, Allocator.TempJob);
                var checkJob = new CheckNearbyEnemyJob
                {
                    selfPositions = team.positions,
                    enemyPositions = enemyTeam.positions,
                    attackRangeSq = attackRange * attackRange,
                    canAttack = attackCheckFlags
                };
                detectionHandle = checkJob.Schedule(team.positions.Length, 32, default);

                var attackJob = new AttackEnemyJob
                {
                    canAttack = attackCheckFlags
                };
                attackHandle= attackJob.Schedule(team.positions.Length, 32, detectionHandle);

                attackCheckFlags.Dispose(attackHandle);


                combinedHandle = JobHandle.CombineDependencies(movementHandle, detectionHandle, attackHandle);
                JobHandle.ScheduleBatchedJobs();
            }
        }
    }

    private void LateUpdate()
    {
        if (teamsCreated) combinedHandle.Complete();
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector2 circle = UnityEngine.Random.insideUnitCircle * spawnArea;
        return new Vector3(circle.x, -0.19f, circle.y);
    }

    void OnDestroy()
    {
        foreach (var team in teamDataList)
        {
            if (team.transforms.isCreated)
                team.transforms.Dispose();
            if (team.positions.IsCreated)
                team.positions.Dispose();
        }
    }
}
