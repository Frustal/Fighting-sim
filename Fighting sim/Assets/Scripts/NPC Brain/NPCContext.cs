using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;

public class NPCContext : MonoBehaviour
{
    public Transform Target { get; set; }
    public float MoveSpeed = 3f;
    public float AttackRange = 2f;
    public int Health = 100;
    public int TeamID; // 0 or 1

    private INPCState currentState;

    public event Action<string, NPCContext> OnStateChanged;
    public event Action<NPCContext> OnMove;
    public event Action<NPCContext> OnAttack;
    public event Action<NPCContext> OnDeath;

    private bool isTakingDamage = false;

    private void Start()
    {
        ChangeState(new IdleState());
    }

    private void Update()
    {
        currentState?.Update(this);
    }

    public void ChangeState(INPCState newState)
    {
        currentState?.Exit(this);
        currentState = newState;
        currentState?.Enter(this);

        // Notify about state change (send state name for example)
        OnStateChanged?.Invoke(newState.GetType().Name, this);
    }

    public Transform FindClosestEnemy()
    {
        var npcs = FindObjectsOfType<NPCContext>();
        float minDist = float.MaxValue;
        Transform closest = null;

        foreach (var npc in npcs)
        {
            if (npc == this) continue;
            if (npc.TeamID == this.TeamID) continue;

            float dist = Vector3.Distance(transform.position, npc.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = npc.transform;
            }

            print("searching");
        }

        return closest;
    }

    public void MoveTowardsTarget()
    {
        if (Target == null) return;

        transform.position = Vector3.MoveTowards(transform.position, Target.position, MoveSpeed * Time.deltaTime);

        OnMove?.Invoke(this);

        print("moving");
    }

    public bool IsInAttackRange()
    {
        if (Target == null) return false;
        return Vector3.Distance(transform.position, Target.position) <= AttackRange;
    }

    public void Attack()
    {
        if (Target.TryGetComponent<NPCContext>(out var targetContext))
        {
            targetContext.TakeDamage(10, 0.3f);

            OnAttack?.Invoke(this);

            print("attacking");
        }
    }

    public void TakeDamage(int amount, float delay = 0.3f)
    {
        if (!isTakingDamage)
        {
            StartCoroutine(DelayedDamage(amount, delay));
        }
    }

    private IEnumerator DelayedDamage(int amount, float delay)
    {
        isTakingDamage = true;

        yield return new WaitForSeconds(delay);

        Health -= amount;

        if (Health <= 0)
        {
            ChangeState(new DeadState());

            OnDeath?.Invoke(this);

        }

        isTakingDamage = false;
    }

    public void Die()
    {
        // Simple death logic
        Destroy(gameObject, 3);
    }
}