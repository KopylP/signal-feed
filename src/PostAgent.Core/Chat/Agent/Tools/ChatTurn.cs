namespace PostAgent.Chat.Agent.Tools;

// One question of the reader as the tools see it (ADR-0021). An invasive change goes into the plan and waits for the owner's tap;
// a non-invasive action is done by its tool at once, except clearing the context, which only the caller can do once the answer is saved.
public sealed class ChatTurn(ProposalPlan plan)
{
    public ProposalPlan Plan => plan;

    // The chat that asked: the profile, the confirmation and the conversation belong to it.
    public long ChatId => plan.ChatId;

    public bool ClearsContext { get; private set; }

    public void ClearContextAfterAnswer() => ClearsContext = true;
}
