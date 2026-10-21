using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Auditing;

internal sealed class AuditLog(AppDbContext db) : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Record(string actorEmail, Guid actorId, string action, string targetType, Guid targetId,
        string? targetLabel, object? before, object? after, string? reason = null) =>
        db.AuditEntries.Add(AuditEntry.Record(actorId, actorEmail, action, targetType, targetId, targetLabel, reason,
            Serialize(before), Serialize(after)));

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, Json);
}
