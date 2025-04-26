public class IdleState : INPCState
{
    public void Enter(NPCContext context) { }
    public void Exit(NPCContext context) { }
    public void Update(NPCContext context)
    {
        // Maybe start searching after some time
        context.ChangeState(new SearchState());
    }
}
