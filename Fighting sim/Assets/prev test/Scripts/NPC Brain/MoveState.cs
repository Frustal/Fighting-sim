public class MoveState : INPCState
{
    public void Enter(NPCContext context) { }
    public void Exit(NPCContext context) { }
    public void Update(NPCContext context)
    {
        /*
        if (context.Target == null)
        {
            context.ChangeState(new SearchState());
            return;
        }

        context.MoveTowardsTarget();

        if (context.IsInAttackRange())
        {
            context.ChangeState(new AttackState());
        }
        */
    }
}
