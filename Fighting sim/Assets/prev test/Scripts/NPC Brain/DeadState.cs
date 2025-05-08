public class DeadState : INPCState
{
    public void Enter(NPCContext context)
    {
        context.Die();
    }

    public void Exit(NPCContext context) { }
    public void Update(NPCContext context) { }
}
