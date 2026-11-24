namespace PostAgent.Models;

public enum SourceType { Channel, Group }

/// <summary>Post — одне повідомлення; Bundle — пачка повідомлень групи.</summary>
public enum PostKind { Post, Bundle }

public enum PostStatus { New, Scored, Sent }
