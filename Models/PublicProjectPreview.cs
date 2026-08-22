namespace MelrandiaManagement.Models;

/// <summary>
/// Public, non-operational project information. Runtime values and secrets
/// must never be added to this contract.
/// </summary>
public sealed record PublicProjectPreview(
    string Id,
    string Slug,
    string ShortName,
    string DisplayName,
    string Summary,
    string Status,
    string Sequence,
    bool IsFeatured);
