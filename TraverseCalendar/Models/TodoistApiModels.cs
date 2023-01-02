namespace TraverseCalendar.Models;

public class Project
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CommentCount { get; set; }
    public long Order { get; set; }
    public string Color { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInboxProject { get; set; }
    public bool IsTeamInbox { get; set; }
    public string ViewStyle { get; set; } = string.Empty;
    public Uri? Url { get; set; }
    public string ParentId { get; set; } = string.Empty;
}

public class Item
{
    public string CreatorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AssigneeId { get; set; } = string.Empty;
    public string AssignerId { get; set; } = string.Empty;
    public int CommentCount { get; set; }
    public bool IsCompleted { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Due Due { get; set; } = new();
    public string Id { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public long Order { get; set; }
    public long Priority { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string SectionId { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public Uri? Url { get; set; }
}

public class Due
{
    public DateTime? Date { get; set; }
    public bool IsRecurring { get; set; }
    public DateTime? Datetime { get; set; }
    public string String { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
}