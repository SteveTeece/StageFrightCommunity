using StageFright.Core.Entities;

namespace StageFright.Core.Contracts;

/// <summary>
/// Repository contract for ExternalOrganisationContact entities. Removing a contact is a soft
/// delete through <see cref="ISoftDeletableRepository{TEntity}.ArchiveAsync"/> (spec 032 FR-009).
/// </summary>
public interface IExternalOrganisationContactRepository : ISoftDeletableRepository<ExternalOrganisationContact>
{
}
