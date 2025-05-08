using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public class ParallelFightSimulator : MonoBehaviour
{
    public GameObject npcPrefab;
    public int teamASize = 200;
    public int teamBSize = 200;
    public float spawnAreaSize = 50f;

    private List<NPC> teamA = new List<NPC>();
    private List<NPC> teamB = new List<NPC>();

    public class NPC
    {
        public GameObject obj;
        public int hp = 100;
        public int team;
        public Vector3 position;
        public Vector3 targetPosition;
        public int targetEnemyIndex = -1;
    }

    void Start()
    {
        SpawnTeams();
    }

    void SpawnTeams()
    {
        // Spawn Team A (red)
        for (int i = 0; i < teamASize; i++)
        {
            var npcObj = Instantiate(npcPrefab, RandomSpawnPosition(), Quaternion.identity);
            npcObj.GetComponent<Renderer>().material.color = Color.red;
            teamA.Add(new NPC
            {
                obj = npcObj,
                team = 0,
                position = npcObj.transform.position
            });
        }

        // Spawn Team B (blue)
        for (int i = 0; i < teamBSize; i++)
        {
            var npcObj = Instantiate(npcPrefab, RandomSpawnPosition(), Quaternion.identity);
            npcObj.GetComponent<Renderer>().material.color = Color.blue;
            teamB.Add(new NPC
            {
                obj = npcObj,
                team = 1,
                position = npcObj.transform.position
            });
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

        // Update positions in main thread first
        UpdateAllPositions();

        // Parallel processing
        Parallel.For(0, teamA.Count, i => {
            if (teamA[i].hp > 0) UpdateNPC(teamA[i], teamB);
        });
        Parallel.For(0, teamB.Count, i => {
            if (teamB[i].hp > 0) UpdateNPC(teamB[i], teamA);
        });

        watch.Stop();
        Debug.Log($"Parallel Update took: {watch.ElapsedMilliseconds} ms");
    }

    void UpdateAllPositions()
    {
        // Update positions in main thread before parallel processing
        for (int i = 0; i < teamA.Count; i++)
        {
            if (teamA[i].hp > 0)
            {
                teamA[i].position = teamA[i].obj.transform.position;
            }
        }

        for (int i = 0; i < teamB.Count; i++)
        {
            if (teamB[i].hp > 0)
            {
                teamB[i].position = teamB[i].obj.transform.position;
            }
        }
    }

    void UpdateNPC(NPC npc, List<NPC> enemies)
    {
        // 1. Find nearest enemy if don't have one
        if (npc.targetEnemyIndex == -1 || enemies[npc.targetEnemyIndex].hp <= 0)
        {
            npc.targetEnemyIndex = FindNearestEnemy(npc, enemies);
        }

        // 2. Move toward enemy or random position if no enemies left
        if (npc.targetEnemyIndex != -1)
        {
            npc.targetPosition = enemies[npc.targetEnemyIndex].position;
        }
        else
        {
            // Wander randomly if no enemies
            if (Vector3.Distance(npc.position, npc.targetPosition) < 1f)
            {
                npc.targetPosition = new Vector3(
                    Random.Range(-spawnAreaSize, spawnAreaSize),
                    0,
                    Random.Range(-spawnAreaSize, spawnAreaSize)
                );
            }
        }

        // 3. Calculate movement (applied in main thread)
        Vector3 moveDirection = (npc.targetPosition - npc.position).normalized;
        npc.position += moveDirection * 2f * Time.deltaTime;

        // 4. Attack if in range
        if (npc.targetEnemyIndex != -1 &&
            Vector3.Distance(npc.position, enemies[npc.targetEnemyIndex].position) < 1.5f)
        {
            // This is safe because we're only modifying primitive values
            enemies[npc.targetEnemyIndex].hp -= 1;
        }
    }

    int FindNearestEnemy(NPC npc, List<NPC> enemies)
    {
        int nearestIndex = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].hp <= 0) continue;

            float distance = Vector3.Distance(npc.position, enemies[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    void LateUpdate()
    {
        // Apply all position changes in main thread
        for (int i = 0; i < teamA.Count; i++)
        {
            if (teamA[i].hp > 0)
            {
                teamA[i].obj.transform.position = teamA[i].position;
                teamA[i].obj.transform.localScale = Vector3.one * (0.5f + teamA[i].hp / 200f);
            }
        }

        for (int i = 0; i < teamB.Count; i++)
        {
            if (teamB[i].hp > 0)
            {
                teamB[i].obj.transform.position = teamB[i].position;
                teamB[i].obj.transform.localScale = Vector3.one * (0.5f + teamB[i].hp / 200f);
            }
        }
    }
}