using PostAgent.Shared.Notifications;

namespace PostAgent.Feedback;

public static class FeedbackServices
{
    public static IServiceCollection AddFeedback(this IServiceCollection services) =>
        services.AddScoped<IIncomingMessageHandler, FeedbackInbox>();
}
