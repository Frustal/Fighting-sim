using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public class NPCContext : MonoBehaviour
{
    public Transform Target { get; set; }
    public float AttackRange = 2f;
    public int Health = 100;
    public int TeamID; // 0 or 1
    public bool CanAttack = false;

    private INPCState currentState;

    public event Action<string, NPCContext> OnStateChanged;
    public event Action<NPCContext> OnMove;
    public event Action<NPCContext> OnAttack;
    public event Action<NPCContext> OnDeath;

    private bool isTakingDamage = false;

    private NPCController controller;

    private static List<NPCContext> damageQueue = new List<NPCContext>();
    private static List<int> damageAmounts = new List<int>();
    private static bool processingDamageThisFrame = false;


    private void Start()
    {
        ChangeState(new IdleState());
        controller = FindObjectOfType<NPCController>();
    }

    private void FixedUpdate()
    {
        ProcessDamageQueue();
    }

    public void UpdateStateBasedOnTarget()
    {

        if (currentState is DeadState) return;

        if (Target == null)
        {
            ChangeState(new IdleState());
        }
        else if (CanAttack)
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
        damageQueue.Add(this);
        damageAmounts.Add(amount);
    }

    public static void ProcessDamageQueue()
    {
        if (processingDamageThisFrame) return;

        processingDamageThisFrame = true;

        for (int i = 0; i < damageQueue.Count; i++)
        {
            var npc = damageQueue[i];
            var amount = damageAmounts[i];

            npc.Health -= amount;

            if (npc.Health <= 0)
            {
                npc.ChangeState(new DeadState());
                npc.OnDeath?.Invoke(npc);
            }
        }

        damageQueue.Clear();
        damageAmounts.Clear();
        processingDamageThisFrame = false;
    }

    public void Die()
    {
        // Simple death logic
        controller?.RemoveNPC(this);
        Destroy(gameObject, 3);
    }
}