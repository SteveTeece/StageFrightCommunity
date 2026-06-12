using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Data.Repositories;

public class ParticipationRepository : SoftDeletableBaseRepository<ParticipationRecord>, IParticipationRepository
{
    public ParticipationRepository(StageFrightDbContext db) : base(db) { }
}
