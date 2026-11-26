using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

/// <summary>
/// Contacts are listed through their organisation's <see cref="ExternalOrganisation.Contacts"/>
/// (see <see cref="ExternalOrganisationRepository"/>); this repository adds, updates and removes them.
/// </summary>
public class ExternalOrganisationContactRepository : SoftDeletableBaseRepository<ExternalOrganisationContact>, IExternalOrganisationContactRepository
{
    public ExternalOrganisationContactRepository(StageFrightDbContext db) : base(db) { }
}
