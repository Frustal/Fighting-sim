public interface INPCState
{
    void Enter(NPCContext context);
    void Exit(NPCContext context);
    void Update(NPCContext context);
}
