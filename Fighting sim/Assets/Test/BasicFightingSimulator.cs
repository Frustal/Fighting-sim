using UnityEngine;
using System.Collections.Generic;

public class BasicFightSimulator : MonoBehaviour
{
    public GameObject npcPrefab;
    public int teamASize = 200;
    public int teamBSize = 200;
    public float spawnAreaSize = 50f;

    private List<GameObject> teamA = new List<GameObject>();
    private List<GameObject> teamB = new List<GameObject>();

    private class NPCData
    {
        public int hp = 100;
        public Vector3 targetPosition;
        public GameObject targetEnemy;
    }

    private Dictionary<GameObject, NPCData> npcData = new Dictionary<GameObject, NPCData>();

    void Start()
    {
        SpawnTeams();
    }

    void SpawnTeams()
    {
        // Spawn Team A (red)
        for (int i = 0; i < teamASize; i++)
        {
            var npc = Instantiate(npcPrefab, RandomSpawnPosition(), Quaternion.identity);
            npc.GetComponent<Renderer>().material.color = Color.red;
            teamA.Add(npc);
            npcData.Add(npc, new NPCData());
        }

        // Spawn Team B (blue)
        for (int i = 0; i < teamBSize; i++)
        {
            var npc = Instantiate(npcPrefab, RandomSpawnPosition(), Quaternion.identity);
            npc.GetComponent<Renderer>().material.color = Color.blue;
            teamB.Add(npc);
            npcData.Add(npc, new NPCData());
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

        // Update Team A
        foreach (var npc in teamA)
        {
            if (npcData[npc].hp <= 0) continue;
            UpdateNPC(npc, teamB);
        }

        // Update Team B
        foreach (var npc in teamB)
        {
            if (npcData[npc].hp <= 0) continue;
            UpdateNPC(npc, teamA);
        }

        // Clean up dead NPCs
        teamA.RemoveAll(npc => npcData[npc].hp <= 0);
        teamB.RemoveAll(npc => npcData[npc].hp <= 0);

        watch.Stop();
        Debug.Log($"Basic Update took: {watch.ElapsedMilliseconds} ms");
    }

    void UpdateNPC(GameObject npc, List<GameObject> enemies)
    {
        var data = npcData[npc];

        // 1. Find nearest enemy if don't have one
        if (data.targetEnemy == null || npcData[data.targetEnemy].hp <= 0)
        {
            data.targetEnemy = FindNearestEnemy(npc, enemies);
        }

        // 2. Move toward enemy or random position if no enemies left
        if (data.targetEnemy != null)
        {
            data.targetPosition = data.targetEnemy.transform.position;
        }
        else
        {
            // Wander randomly if no enemies
            if (Vector3.Distance(npc.transform.position, data.targetPosition) < 1f)
            {
                data.targetPosition = RandomSpawnPosition();
            }
        }

        // 3. Move toward target
        Vector3 moveDirection = (data.targetPosition - npc.transform.position).normalized;
        npc.transform.position += moveDirection * 2f * Time.deltaTime;

        // 4. Attack if in range
        if (data.targetEnemy != null &&
            Vector3.Distance(npc.transform.position, data.targetEnemy.transform.position) < 1.5f)
        {
            npcData[data.targetEnemy].hp -= 1;

            // Visual feedback
            data.targetEnemy.transform.localScale = Vector3.one * (0.5f + npcData[data.targetEnemy].hp / 200f);
        }
    }

    GameObject FindNearestEnemy(GameObject npc, List<GameObject> enemies)
    {
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (npcData[enemy].hp <= 0) continue;

            float distance = Vector3.Distance(npc.transform.position, enemy.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = enemy;
            }
        }

        return nearest;
    }
}