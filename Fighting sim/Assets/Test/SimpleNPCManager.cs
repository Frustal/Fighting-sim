using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class SimpleNPCManager : MonoBehaviour
{
    public GameObject npcTeam1;
    public GameObject npcTeam2;
    public int teamSize = 200;
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1f;
    public float spawnRange = 20f;

    private List<NPC> npcs = new List<NPC>();
    private bool spawningComplete = false;

    private class NPC
    {
        public Transform transform;
        public Animator animator;
        public int hp;
        public int team;
        public float lastAttackTime;
        public AnimationState currentState;
    }

    private enum AnimationState
    {
        Moving,
        Attacking,
        Dying
    }

    void Start()
    {
        StartCoroutine(SpawnNPCs());
    }

    IEnumerator SpawnNPCs()
    {
        int total = teamSize * 2;

        for (int i = 0; i < total; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-spawnRange, spawnRange), 0, Random.Range(-spawnRange, spawnRange));
            GameObject prefab = i < teamSize ? npcTeam1 : npcTeam2;
            GameObject npcObj = Instantiate(prefab, pos, Quaternion.identity);
            npcObj.name = $"NPC_{i}";

            npcs.Add(new NPC
            {
                transform = npcObj.transform,
                animator = npcObj.GetComponent<Animator>(),
                hp = 100,
                team = i < teamSize ? 0 : 1,
                lastAttackTime = -attackCooldown,
                currentState = AnimationState.Moving
            });

            if (i % 50 == 0)
                yield return null;
        }

        spawningComplete = true;
    }

    void Update()
    {
        if (!spawningComplete) return;

        // Process NPCs in batches across frames to spread CPU load
        int batchSize = Mathf.Max(1, npcs.Count / 4); // Process 1/4 of NPCs each frame
        int startIndex = (Time.frameCount % 4) * batchSize;
        int endIndex = Mathf.Min(startIndex + batchSize, npcs.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            NPC npc = npcs[i];
            if (npc.hp <= 0)
            {
                if (npc.currentState != AnimationState.Dying)
                {
                    npc.currentState = AnimationState.Dying;
                    PlayAnimation(npc, "Death");
                }
                continue;
            }

            // Find closest enemy
            NPC closestEnemy = FindClosestEnemy(npc, i);
            if (closestEnemy != null)
            {
                float distance = Vector3.Distance(npc.transform.position, closestEnemy.transform.position);

                if (distance <= attackRange)
                {
                    // Attack if cooldown is over
                    if (Time.time - npc.lastAttackTime >= attackCooldown)
                    {
                        closestEnemy.hp -= (int)attackDamage;
                        npc.lastAttackTime = Time.time;
                        npc.currentState = AnimationState.Attacking;
                        PlayAnimation(npc, "Attack");
                    }
                }
                else
                {
                    // Move toward enemy
                    Vector3 dir = (closestEnemy.transform.position - npc.transform.position).normalized;
                    npc.transform.position += dir * moveSpeed * Time.deltaTime;
                    npc.currentState = AnimationState.Moving;
                    PlayAnimation(npc, "Move");
                }
            }
            else
            {
                // No target found - idle
                npc.currentState = AnimationState.Moving;
                PlayAnimation(npc, "Move");
            }
        }

        // Remove dead NPCs (optional)
        npcs.RemoveAll(npc => npc.hp <= 0 && npc.currentState == AnimationState.Dying && !IsAnimationPlaying(npc.animator, "Death"));
    }

    private NPC FindClosestEnemy(NPC currentNPC, int currentIndex)
    {
        NPC closestEnemy = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < npcs.Count; i++)
        {
            if (i == currentIndex || npcs[i].hp <= 0 || npcs[i].team == currentNPC.team)
                continue;

            float dist = Vector3.SqrMagnitude(npcs[i].transform.position - currentNPC.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestEnemy = npcs[i];
            }
        }

        return closestEnemy;
    }

    private void PlayAnimation(NPC npc, string animationName)
    {
        if (npc.animator != null && !IsAnimationPlaying(npc.animator, animationName))
        {
            npc.animator.Play(animationName);
        }
    }

    private bool IsAnimationPlaying(Animator animator, string animationName)
    {
        if (animator == null) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName(animationName);
    }

    void OnDestroy()
    {
        // Clean up all NPC GameObjects
        foreach (NPC npc in npcs)
        {
            if (npc.transform != null)
            {
                Destroy(npc.transform.gameObject);
            }
        }
        npcs.Clear();
    }
}