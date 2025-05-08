using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;
using System.Collections;
using System.Threading.Tasks;

public enum AnimationState
{
    Moving,
    Attacking,
    Dying
}

public class NPCManager : MonoBehaviour
{
    public GameObject npcTeam1;
    public GameObject npcTeam2;
    public int teamSize = 200;
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float moveSpeed = 2f;
    public float attackCooldown = 1f;
    public float spawnRange = 20f;

    private TransformAccessArray npcTransforms;
    private NativeArray<Vector3> positions;
    private NativeArray<int> hp;
    private NativeArray<int> team;
    private NativeArray<float> lastAttackTime;
    private NativeArray<AttackResult> attackResults;
    private NativeArray<AnimationState> animationStates;

    private JobHandle jobHandle;
    private bool spawned = false;

    private async void Start()
    {
        await SpawnNPCsAsync();
    }

    private async Task SpawnNPCsAsync()
    {
        int total = teamSize * 2;
        npcTransforms = new TransformAccessArray(total);
        positions = new NativeArray<Vector3>(total, Allocator.Persistent);
        hp = new NativeArray<int>(total, Allocator.Persistent);
        team = new NativeArray<int>(total, Allocator.Persistent);
        lastAttackTime = new NativeArray<float>(total, Allocator.Persistent);
        attackResults = new NativeArray<AttackResult>(total, Allocator.Persistent);
        animationStates = new NativeArray<AnimationState>(total, Allocator.Persistent);

        for (int i = 0; i < total; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-spawnRange, spawnRange), 0, Random.Range(-spawnRange, spawnRange));
            GameObject prefab = i < teamSize ? npcTeam1 : npcTeam2;
            GameObject npc = Instantiate(prefab, pos, Quaternion.identity);
            npc.name = $"NPC_{i}";
            npcTransforms.Add(npc.transform);
            positions[i] = pos;
            hp[i] = 100;
            team[i] = (i < teamSize) ? 0 : 1;
            lastAttackTime[i] = -attackCooldown;
            attackResults[i] = new AttackResult { targetIndex = -1, damage = 0 };
            animationStates[i] = AnimationState.Moving;

            if (i % 50 == 0)
                await Task.Yield();
        }
        spawned = true;
    }

    void Update()
    {
        if (!spawned || npcTransforms.length == 0)
            return;

        // creating copy of positions for read only access in the job
        var positionsCopy = new NativeArray<Vector3>(positions, Allocator.TempJob);

        var job = new NPCLogicJob
        {
            deltaTime = Time.deltaTime,
            time = Time.time,
            moveSpeed = moveSpeed,
            attackRange = attackRange,
            attackDamage = attackDamage,
            attackCooldown = attackCooldown,
            positions = positionsCopy,
            hp = hp,
            team = team,
            lastAttackTime = lastAttackTime,
            attackResults = attackResults,
            animationStates = animationStates
        };

        jobHandle = job.Schedule(npcTransforms);

        //disposes after job is complete
        jobHandle = positionsCopy.Dispose(jobHandle);
    }

    void LateUpdate()
    {
        if (!spawned || npcTransforms.length == 0)
            return;

        jobHandle.Complete();

        for (int i = 0; i < npcTransforms.length; i++)
        {
            positions[i] = npcTransforms[i].position;

            /*
            var animator = npcTransforms[i].GetComponent<Animator>();
            if (animator != null)
            {
                if (animationStates[i] == AnimationState.Moving) animator.Play("Move");
                if (animationStates[i] == AnimationState.Dying) animator.Play("Death");
                if (animationStates[i] == AnimationState.Attacking) animator.Play("Attack");
            }
            */

            if (hp[i] <= 0 && npcTransforms[i].gameObject.activeSelf)
            {
                npcTransforms[i].gameObject.SetActive(false);
            }
        }

        // if multiple NPCs attacked the same target,
        // each attack result reduces that target's hp
        for (int i = 0; i < attackResults.Length; i++)
        {
            AttackResult ar = attackResults[i];
            if (ar.targetIndex >= 0)
            {
                hp[ar.targetIndex] = Mathf.Max(0, hp[ar.targetIndex] - ar.damage);
            }
            attackResults[i] = new AttackResult { targetIndex = -1, damage = 0 };
        }
    }

    void OnDestroy()
    {
        if (npcTransforms.isCreated) npcTransforms.Dispose();
        if (positions.IsCreated) positions.Dispose();
        if (hp.IsCreated) hp.Dispose();
        if (team.IsCreated) team.Dispose();
        if (lastAttackTime.IsCreated) lastAttackTime.Dispose();
        if (attackResults.IsCreated) attackResults.Dispose();
        if (animationStates.IsCreated) animationStates.Dispose();
    }

    // double buffering damage application.
    public struct AttackResult
    {
        public int targetIndex;
        public int damage;
    }

    [BurstCompile]
    struct NPCLogicJob : IJobParallelForTransform
    {
        public float deltaTime;
        public float time;
        public float moveSpeed;
        public float attackRange;
        public float attackDamage;
        public float attackCooldown;

        [ReadOnly] public NativeArray<Vector3> positions;
        [ReadOnly] public NativeArray<int> team;
        [ReadOnly] public NativeArray<int> hp;
        public NativeArray<float> lastAttackTime;
        public NativeArray<AttackResult> attackResults;
        public NativeArray<AnimationState> animationStates;

        public void Execute(int index, TransformAccess transform)
        {
            if (hp[index] <= 0)
            {
                attackResults[index] = new AttackResult { targetIndex = -1, damage = 0 };
                animationStates[index] = AnimationState.Dying;
                return;
            }

            Vector3 myPos = transform.position;
            Vector3 targetPos = myPos;
            float closestDist = float.MaxValue;
            int targetIndex = -1;

            // finding closest enemy
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == index || hp[i] <= 0 || team[i] == team[index])
                    continue;

                float dist = (positions[i] - myPos).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetPos = positions[i];
                    targetIndex = i;
                }
            }

            // initializing ar
            animationStates[index] = AnimationState.Moving;
            attackResults[index] = new AttackResult { targetIndex = -1, damage = 0 };

            if (targetIndex >= 0)
            {
                float sqrAttackRange = attackRange * attackRange;
                if (closestDist <= sqrAttackRange)
                {
                    if (time - lastAttackTime[index] >= attackCooldown)
                    {
                        attackResults[index] = new AttackResult { targetIndex = targetIndex, damage = (int)attackDamage };
                        lastAttackTime[index] = time;
                        animationStates[index] = AnimationState.Attacking;
                    }
                }
                else
                {
                    // moving
                    Vector3 dir = (targetPos - myPos).normalized;
                    transform.position += dir * moveSpeed * deltaTime;
                    attackResults[index] = new AttackResult { targetIndex = -1, damage = 0 };
                    animationStates[index] = AnimationState.Moving;
                }
            }
            else
            {
                // no target found
                attackResults[index] = new AttackResult { targetIndex = -1, damage = 0 };
                animationStates[index] = AnimationState.Moving;
            }
        }
    }
}
