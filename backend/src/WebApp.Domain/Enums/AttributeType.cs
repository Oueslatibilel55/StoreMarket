namespace WebApp.Domain.Enums;

/// <summary>Kind of value a product attribute holds. Stored as int — append only, never renumber.</summary>
public enum AttributeType
{
    Text = 1,
    Number = 2,
    /// <summary>Yes / no.</summary>
    Boolean = 3,
    SingleChoice = 4,
    MultiChoice = 5,
}
