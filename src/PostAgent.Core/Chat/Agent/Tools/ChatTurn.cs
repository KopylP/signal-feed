using PostAgent.Chat.Memory;

namespace PostAgent.Chat.Agent.Tools;

// One question of the reader as the tools see it (ADR-0021). An invasive change goes into the plan and waits for the owner's tap;
// a non-invasive action is done by its tool at once, except clearing the context, which only the caller can do once the answer is saved.
public sealed class ChatTurn(ProposalPlan plan)
{
    public ProposalPlan Plan => plan;

    private readonly List<string> _memoryNotices = [];

    // The chat that asked: the reader memory, the confirmation and the conversation belong to it.
    public long ChatId => plan.ChatId;

    public bool ClearsContext { get; private set; }

    // The options shown as buttons under the answer (offer_choices); a later call replaces an earlier one.
    public IReadOnlyList<string>? Choices { get; private set; }

    // One line per change of the reader memory in this turn, built by code, shown under the answer (ADR-0022).
    public IReadOnlyList<string> MemoryNotices => _memoryNotices;

    public void RecordMemory(MemoryEdit edit)
    {
        if (edit.Notice is { } notice)
        {
            _memoryNotices.Add(notice);
        }
    }

    public void ClearContextAfterAnswer() => ClearsContext = true;

    public void OfferChoices(IReadOnlyList<string> options) => Choices = options;
}
