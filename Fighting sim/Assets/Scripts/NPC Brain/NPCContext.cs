using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;

public class NPCContext : MonoBehaviour
{
    public Transform Target { get; set; }
    public float AttackRange = 2f;
    public int Health = 100;
    public int TeamID; // 0 or 1

    private INPCState currentState;

    public event Action<string, NPCContext> OnStateChanged;
    public event Action<NPCContext> OnMove;
    public event Action<NPCContext> OnAttack;
    public event Action<NPCContext> OnDeath;

    private bool isTakingDamage = false;

    private NPCController controller;

    private void Start()
    {
        ChangeState(new IdleState());
        controller = FindObjectOfType<NPCController>();
    }

    /*
    private void Update()
    {
        currentState?.Update(this);
    }
    */

    public void UpdateStateBasedOnTarget()
    {

        if (currentState is DeadState) return;

        if (Target == null)
        {
            ChangeState(new IdleState());
        }
        else if (IsInAttackRange())
        {
            ChangeState(new AttackState());
        }
        else
        {
            ChangeState(new MoveState());
            OnMove?.Invoke(this);
        }

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
        print("taking damage");
        isTakingDamage = false;
    }

    public void Die()
    {
        // Simple death logic
        controller?.RemoveNPC(this);
        Destroy(gameObject, 3);
    }
}