namespace PostAgent.Collecting;

public sealed class SourceThrottledException(string message, Exception inner) : Exception(message, inner);
