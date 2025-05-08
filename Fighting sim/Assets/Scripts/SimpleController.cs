using System.Collections.Generic;
using UnityEngine;

public class SimpleController : MonoBehaviour
{
    [SerializeField] private projectAnimController animController;

    [Header("Setup")]
    public GameObject npcPrefab;
    public GameObject enemyPrefab;
    public int numberOfNPCs = 3;
    public int numberOfEnemies = 3;
    public float moveSpeed = 5f;
    public float stopDistance = 1.0f;
    public float spawnArea = 10f;

    [SerializeField] private List<NPCContext> npcContexts = new List<NPCContext>();
    [SerializeField] private List<NPCContext> enemyContexts = new List<NPCContext>();

    void Start()
    {
        SpawnCharacters();
        animController.AssignNPCS();
    }

    void SpawnCharacters()
    {
        for (int i = 0; i < numberOfNPCs; i++)
        {
            GameObject go = Instantiate(npcPrefab, GetRandomSpawnPosition(), Quaternion.identity);
            var context = go.GetComponent<NPCContext>();
            context.TeamID = 0;
            npcContexts.Add(context);
        }

        for (int i = 0; i < numberOfEnemies; i++)
        {
            GameObject go = Instantiate(enemyPrefab, GetRandomSpawnPosition(), Quaternion.identity);
            var context = go.GetComponent<NPCContext>();
            context.TeamID = 1;
            enemyContexts.Add(context);
        }
    }

    void Update()
    {
        UpdateNPCMovement();
        UpdateNPCStates();
    }

    void UpdateNPCMovement()
    {
        // Move NPCs towards enemies
        foreach (var npc in npcContexts)
        {
            if (npc == null || npc.Target == null) continue;

            float distance = Vector3.Distance(npc.transform.position, npc.Target.position);
            if (distance > stopDistance)
            {
                Vector3 direction = (npc.Target.position - npc.transform.position).normalized;
                npc.transform.position += direction * moveSpeed * Time.deltaTime;
                npc.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // Move enemies towards NPCs
        foreach (var enemy in enemyContexts)
        {
            if (enemy == null || enemy.Target == null) continue;

            float distance = Vector3.Distance(enemy.transform.position, enemy.Target.position);
            if (distance > stopDistance)
            {
                Vector3 direction = (enemy.Target.position - enemy.transform.position).normalized;
                enemy.transform.position += direction * moveSpeed * Time.deltaTime;
                enemy.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    void UpdateNPCStates()
    {
        // Update NPC states
        foreach (var npc in npcContexts)
        {
            if (npc == null) continue;
            npc.Target = FindClosestEnemy(npc.transform, enemyContexts);
            npc.UpdateStateBasedOnTarget();
        }

        // Update enemy states
        foreach (var enemy in enemyContexts)
        {
            if (enemy == null) continue;
            enemy.Target = FindClosestEnemy(enemy.transform, npcContexts);
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
        if (npc.TeamID == 0)
        {
            npcContexts.Remove(npc);
            numberOfNPCs--;
        }
        else
        {
            enemyContexts.Remove(npc);
            numberOfEnemies--;
        }

        Destroy(npc.gameObject);
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spawnArea;
        return new Vector3(randomCircle.x, 1.46f, randomCircle.y);
    }
}