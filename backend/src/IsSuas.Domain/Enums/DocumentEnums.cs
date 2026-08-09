namespace IsSuas.Domain.Enums;

public enum DocumentStatus
{
    Draft = 0,
    Current = 1,
    Superseded = 2,
    Expired = 3,
    Archived = 4
}

public enum DocumentSensitivity
{
    PublicInternal = 0,
    Internal = 1,
    Sensitive = 2,
    Restricted = 3
}

public enum FormTemplateKind
{
    Checklist = 0,
    Form = 1,
    RiskAssessment = 2
}

public enum FormItemType
{
    Text = 0,
    Number = 1,
    Date = 2,
    Select = 3,
    YesNo = 4,
    Attachment = 5,
    Signature = 6
}

public enum FormSubmissionStatus
{
    Draft = 0,
    Completed = 1,
    Cancelled = 2
}

public enum RiskClassification
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum LinkedEntityType
{
    OrgUnit = 0,
    Uas = 1,
    Pilot = 2,
    Operation = 3,
    Flight = 4,
    Battery = 5,
    Component = 6,
    SafetyEvent = 7,
    WorkOrder = 8
}
