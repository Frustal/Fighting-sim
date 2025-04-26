public class AttackState : INPCState
{
    public void Enter(NPCContext context) { }
    public void Exit(NPCContext context) { }
    public void Update(NPCContext context)
    {
        if (context.Target == null)
        {
            context.ChangeState(new SearchState());
            return;
        }

        context.Attack();

        if (!context.IsInAttackRange())
        {
            context.ChangeState(new MoveState());
        }
    }
}
