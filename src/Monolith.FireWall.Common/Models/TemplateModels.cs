namespace Monolith.FireWall.Common.Models;

public record TemplateDefinition(
    string Id,
    string ResourcePath,
    string OutputPath,
    bool RequiresRoot
);
