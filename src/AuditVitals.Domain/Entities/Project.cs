namespace AuditVitals.Domain.Entities;

/// <summary>
/// A website being tracked for audits. <see cref="UserId"/> is nullable because
/// Milestone 1 ships the public "free audit" flow before authentication exists;
/// anonymous submissions create a project with no owner.
/// </summary>
public sealed class Project
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string RootUrl { get; private set; } = string.Empty;
    public string NormalizedHost { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastAuditAtUtc { get; private set; }

    private Project()
    {
    }

    public static Project Create(string name, string rootUrl, string normalizedHost, Guid? userId, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedHost);

        return new Project
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            RootUrl = rootUrl,
            NormalizedHost = normalizedHost,
            CreatedAtUtc = nowUtc,
        };
    }

    public void RecordAuditStarted(DateTime nowUtc)
    {
        LastAuditAtUtc = nowUtc;
    }
}
