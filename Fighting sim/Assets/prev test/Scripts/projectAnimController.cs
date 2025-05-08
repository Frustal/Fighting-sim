using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class projectAnimController : MonoBehaviour
{

    [SerializeField] private NPCContext[] npcs;
    private int[] team1; //I'll store IDs of teams in these lists
    private int[] team2;

    // Start is called before the first frame update
    public void AssignNPCS()
    {
        npcs = FindObjectsOfType<NPCContext>();
        for(int i = 0; i < npcs.Length; i++)
        {
            npcs[i].OnDeath+= DeathEvent;
            npcs[i].OnMove += MoveEvent;
            npcs[i].OnAttack += AttackEvent;
            npcs[i].OnStateChanged += StateChangeEvent;
        }
    }

    void OnDisable()
    {
        if (npcs != null)
        {
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null)
                {
                    npcs[i].OnDeath -= DeathEvent;
                    npcs[i].OnMove -= MoveEvent;
                    npcs[i].OnAttack -= AttackEvent;
                    npcs[i].OnStateChanged -= StateChangeEvent;
                }
            }
        }
    }

    void DeathEvent(NPCContext npc)
    {
        npc.GetComponent<Animator>().Play("Death");
        npc.OnDeath -= DeathEvent; 
        npc.OnMove -= MoveEvent;
        npc.OnAttack -= AttackEvent;
        npc.OnStateChanged -= StateChangeEvent;
    }

    void MoveEvent(NPCContext npc)
    {
        npc.GetComponent<Animator>().Play("Move");
        return;
    }

    void AttackEvent(NPCContext npc)
    {
        npc.GetComponent<Animator>().Play("Attack");
        Debug.Log("Attack event triggered");
        return;
    }

    void StateChangeEvent(string obj, NPCContext npc)
    {
        if (obj == "IdleState")
        {
            npc.GetComponent<Animator>().Play("Idle");
        }
        return;
    }



}
