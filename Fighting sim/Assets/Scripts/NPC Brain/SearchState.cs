public class SearchState : INPCState
{
    public void Enter(NPCContext context) { }
    public void Exit(NPCContext context) { }
    public void Update(NPCContext context)
    {
        // Find closest enemy
        /*
        var target = context.FindClosestEnemy();
        if (target != null)
        {
            context.Target = target;
            context.ChangeState(new MoveState());
        }
        */
    }
}
