using LON.Application.Common.Importing;
using LON.Application.Common.Models;
using MediatR;

namespace LON.Application.Importing.Queries.GetImportTargets;

public record GetImportTargetsQuery : IRequest<Result<List<ImportTargetSchemaDto>>>;

public record GetImportTargetQuery(string TargetName) : IRequest<Result<ImportTargetSchemaDto>>;

public sealed record ImportTargetSchemaDto(
    string TargetName,
    string DisplayLabel,
    IReadOnlyList<ImportTargetField> Fields);

public class GetImportTargetsQueryHandler
    : IRequestHandler<GetImportTargetsQuery, Result<List<ImportTargetSchemaDto>>>,
      IRequestHandler<GetImportTargetQuery, Result<ImportTargetSchemaDto>>
{
    private readonly IImportTargetRegistry _registry;

    public GetImportTargetsQueryHandler(IImportTargetRegistry registry)
    {
        _registry = registry;
    }

    public Task<Result<List<ImportTargetSchemaDto>>> Handle(
        GetImportTargetsQuery request, CancellationToken cancellationToken)
    {
        var list = _registry.All
            .Select(s => new ImportTargetSchemaDto(s.TargetName, s.DisplayLabel, s.Fields))
            .ToList();
        return Task.FromResult(Result<List<ImportTargetSchemaDto>>.Success(list));
    }

    public Task<Result<ImportTargetSchemaDto>> Handle(
        GetImportTargetQuery request, CancellationToken cancellationToken)
    {
        var schema = _registry.Find(request.TargetName);
        if (schema is null)
            return Task.FromResult(Result<ImportTargetSchemaDto>.Failure(
                $"Unknown import target '{request.TargetName}'. Available: {string.Join(", ", _registry.All.Select(s => s.TargetName))}."));
        return Task.FromResult(Result<ImportTargetSchemaDto>.Success(
            new ImportTargetSchemaDto(schema.TargetName, schema.DisplayLabel, schema.Fields)));
    }
}
