using MapsterMapper;
using Homewatch.Contracts.Abstractions.Repositories;
using Homewatch.Contracts.Dtos.SmokeAlarmDetectorDtos;
using Homewatch.Infrastructure.Data;
using Homewatch.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homewatch.Infrastructure.Repositories
{
    public class SmokeAlarmTelemetryRepository : ISmokeAlarmTelemetryRepository
    {
        private readonly IDbContextFactory<HomewatchDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public SmokeAlarmTelemetryRepository(IDbContextFactory<HomewatchDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<bool> SavePayloadAsync(SmokeAlarmDetectorPayloadDto smokeAlarmDetectorPayload)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            dbContext.SmokeAlarmTelemetry.Add(_mapper.Map<SmokeAlarmTelemetry>(smokeAlarmDetectorPayload));

            return await dbContext.SaveChangesAsync() == 1;
        }

        public async Task<SmokeAlarmDetectorPayloadDto?> GetLatestTelemetryForDetectorAsync(Guid deviceId)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var latestTelemetry = await dbContext.SmokeAlarmTelemetry
                .AsNoTracking()
                .Where(t => t.DeviceId == deviceId)
                .OrderByDescending(t => t.LoggedTimeUTC)
                .Select(x => _mapper.Map<SmokeAlarmDetectorPayloadDto>(x))
                .FirstOrDefaultAsync();

            return latestTelemetry;
        }
    }
}
