using IsSuas.Domain.Common;

namespace IsSuas.Domain.Common;

public class LookupCode : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public string? DescriptionSk { get; set; }
    public ICollection<LookupItem> Items { get; set; } = new List<LookupItem>();
}

public class LookupItem : AuditableEntity
{
    public Guid LookupCodeId { get; set; }
    public LookupCode LookupCode { get; set; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public string LabelSk { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? MetadataJson { get; set; }
}

public class ConfigRule : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public int Version { get; set; } = 1;
    public string? DescriptionSk { get; set; }
}
