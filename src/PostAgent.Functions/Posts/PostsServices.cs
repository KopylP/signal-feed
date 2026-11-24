namespace PostAgent.Posts;

public static class PostsServices
{
    public static IServiceCollection AddPosts(this IServiceCollection services) =>
        services.AddSingleton<IPostRepository, PostRepository>();
}
