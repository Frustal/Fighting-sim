using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class JobFightSimulator : MonoBehaviour
{
    public GameObject npcPrefab;
    public int teamASize = 200;
    public int teamBSize = 200;
    public float spawnAreaSize = 50f;
    public float moveSpeed = 2f;
    public float attackRange = 1.5f;

    private List<GameObject> teamAObjects = new List<GameObject>();
    private List<GameObject> teamBObjects = new List<GameObject>();
    private NativeArray<float3> teamAPositions;
    private NativeArray<float3> teamBPositions;
    private NativeArray<float3> teamATargetPositions;
    private NativeArray<float3> teamBTargetPositions;
    private NativeArray<int> teamAHealth;
    private NativeArray<int> teamBHealth;
    private NativeArray<int> teamATargetIndices;
    private NativeArray<int> teamBTargetIndices;
    private NativeArray<Unity.Mathematics.Random> teamARandom;
    private NativeArray<Unity.Mathematics.Random> teamBRandom;

    void Start()
    {
        InitializeArrays();
        SpawnTeams();
    }

    void InitializeArrays()
    {
        teamAPositions = new NativeArray<float3>(teamASize, Allocator.Persistent);
        teamBPositions = new NativeArray<float3>(teamBSize, Allocator.Persistent);
        teamATargetPositions = new NativeArray<float3>(teamASize, Allocator.Persistent);
        teamBTargetPositions = new NativeArray<float3>(teamBSize, Allocator.Persistent);
        teamAHealth = new NativeArray<int>(teamASize, Allocator.Persistent);
        teamBHealth = new NativeArray<int>(teamBSize, Allocator.Persistent);
        teamATargetIndices = new NativeArray<int>(teamASize, Allocator.Persistent);
        teamBTargetIndices = new NativeArray<int>(teamBSize, Allocator.Persistent);
        teamARandom = new NativeArray<Unity.Mathematics.Random>(teamASize, Allocator.Persistent);
        teamBRandom = new NativeArray<Unity.Mathematics.Random>(teamBSize, Allocator.Persistent);

        for (int i = 0; i < teamASize; i++)
        {
            teamAHealth[i] = 100;
            teamATargetIndices[i] = -1;
            teamARandom[i] = new Unity.Mathematics.Random((uint)(i + 1));
        }

        for (int i = 0; i < teamBSize; i++)
        {
            teamBHealth[i] = 100;
            teamBTargetIndices[i] = -1;
            teamBRandom[i] = new Unity.Mathematics.Random((uint)(i + teamASize + 1));
        }
    }

    void SpawnTeams()
    {
        for (int i = 0; i < teamASize; i++)
        {
            var pos = RandomSpawnPosition();
            var npcObj = Instantiate(npcPrefab, pos, Quaternion.identity);
            npcObj.GetComponent<Renderer>().material.color = Color.red;
            teamAObjects.Add(npcObj);
            teamAPositions[i] = pos;
            teamATargetPositions[i] = pos;
        }

        for (int i = 0; i < teamBSize; i++)
        {
            var pos = RandomSpawnPosition();
            var npcObj = Instantiate(npcPrefab, pos, Quaternion.identity);
            npcObj.GetComponent<Renderer>().material.color = Color.blue;
            teamBObjects.Add(npcObj);
            teamBPositions[i] = pos;
            teamBTargetPositions[i] = pos;
        }
    }

    Vector3 RandomSpawnPosition()
    {
        return new Vector3(
            Random.Range(-spawnAreaSize, spawnAreaSize),
            0,
            Random.Range(-spawnAreaSize, spawnAreaSize)
        );
    }
    void Update()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // Schedule target finding jobs
        var findTargetsJobA = new FindTargetsJob
        {
            myPositions = teamAPositions,
            enemyPositions = teamBPositions,
            enemyHealth = teamBHealth,
            targetIndices = teamATargetIndices
        };
        JobHandle findTargetsHandleA = findTargetsJobA.Schedule(teamASize, default);

        var findTargetsJobB = new FindTargetsJob
        {
            myPositions = teamBPositions,
            enemyPositions = teamAPositions,
            enemyHealth = teamAHealth,
            targetIndices = teamBTargetIndices
        };
        JobHandle findTargetsHandleB = findTargetsJobB.Schedule(teamBSize, default, findTargetsHandleA);

        // Schedule movement jobs
        var moveJobA = new MoveAndAttackJob
        {
            deltaTime = Time.deltaTime,
            moveSpeed = moveSpeed,
            attackRange = attackRange,
            myPositions = teamAPositions,
            myHealth = teamAHealth,
            targetPositions = teamATargetPositions,
            targetIndices = teamATargetIndices,
            enemyPositions = teamBPositions,
            enemyHealth = teamBHealth,
            spawnAreaSize = spawnAreaSize,
            random = teamARandom
        };
        JobHandle moveHandleA = moveJobA.Schedule(teamASize, default, findTargetsHandleB);

        var moveJobB = new MoveAndAttackJob
        {
            deltaTime = Time.deltaTime,
            moveSpeed = moveSpeed,
            attackRange = attackRange,
            myPositions = teamBPositions,
            myHealth = teamBHealth,
            targetPositions = teamBTargetPositions,
            targetIndices = teamBTargetIndices,
            enemyPositions = teamAPositions,
            enemyHealth = teamAHealth,
            spawnAreaSize = spawnAreaSize,
            random = teamBRandom
        };
        JobHandle moveHandleB = moveJobB.Schedule(teamBSize, default, moveHandleA);

        moveHandleB.Complete();
        UpdateVisuals();

        watch.Stop();
        Debug.Log($"Job System Update took: {watch.ElapsedMilliseconds} ms");
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < teamAObjects.Count; i++)
        {
            if (teamAHealth[i] > 0)
            {
                teamAObjects[i].transform.position = teamAPositions[i];
                teamAObjects[i].transform.localScale = Vector3.one * (0.5f + teamAHealth[i] / 200f);
            }
            else if (teamAObjects[i].activeSelf)
            {
                teamAObjects[i].SetActive(false);
            }
        }

        for (int i = 0; i < teamBObjects.Count; i++)
        {
            if (teamBHealth[i] > 0)
            {
                teamBObjects[i].transform.position = teamBPositions[i];
                teamBObjects[i].transform.localScale = Vector3.one * (0.5f + teamBHealth[i] / 200f);
            }
            else if (teamBObjects[i].activeSelf)
            {
                teamBObjects[i].SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        teamAPositions.Dispose();
        teamBPositions.Dispose();
        teamATargetPositions.Dispose();
        teamBTargetPositions.Dispose();
        teamAHealth.Dispose();
        teamBHealth.Dispose();
        teamATargetIndices.Dispose();
        teamBTargetIndices.Dispose();
        teamARandom.Dispose();
        teamBRandom.Dispose();
    }

    struct FindTargetsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> myPositions;
        [ReadOnly] public NativeArray<float3> enemyPositions;
        [ReadOnly] public NativeArray<int> enemyHealth;
        public NativeArray<int> targetIndices;

        public void Execute(int index)
        {
            if (enemyHealth.Length == 0) return;

            if (targetIndices[index] == -1 || enemyHealth[targetIndices[index]] <= 0)
            {
                float minDistance = float.MaxValue;
                int closestIndex = -1;

                for (int i = 0; i < enemyPositions.Length; i++)
                {
                    if (enemyHealth[i] <= 0) continue;

                    float distance = math.distance(myPositions[index], enemyPositions[i]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestIndex = i;
                    }
                }

                targetIndices[index] = closestIndex;
            }
        }
    }

    struct MoveAndAttackJob : IJobParallelFor
    {
        public float deltaTime;
        public float moveSpeed;
        public float attackRange;
        public float spawnAreaSize;
        public NativeArray<float3> myPositions;
        public NativeArray<int> myHealth;
        public NativeArray<float3> targetPositions;
        public NativeArray<int> targetIndices;
        [ReadOnly] public NativeArray<float3> enemyPositions;
        public NativeArray<int> enemyHealth;
        public NativeArray<Unity.Mathematics.Random> random;

        public void Execute(int index)
        {
            if (index >= myHealth.Length || myHealth[index] <= 0) return;

            if (targetIndices[index] != -1 && targetIndices[index] < enemyPositions.Length)
            {
                targetPositions[index] = enemyPositions[targetIndices[index]];
            }
            else
            {
                if (math.distance(myPositions[index], targetPositions[index]) < 1f)
                {
                    var r = random[index];
                    targetPositions[index] = new float3(
                        r.NextFloat(-spawnAreaSize, spawnAreaSize),
                        0,
                        r.NextFloat(-spawnAreaSize, spawnAreaSize)
                    );
                    random[index] = r;
                }
            }

            float3 moveDirection = math.normalize(targetPositions[index] - myPositions[index]);
            myPositions[index] += moveDirection * moveSpeed * deltaTime;

            if (targetIndices[index] != -1 &&
                targetIndices[index] < enemyHealth.Length &&
                math.distance(myPositions[index], enemyPositions[targetIndices[index]]) < attackRange)
            {
                enemyHealth[targetIndices[index]] -= 1;
            }
        }
    }
}