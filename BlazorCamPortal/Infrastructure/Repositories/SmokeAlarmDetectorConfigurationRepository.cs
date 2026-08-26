using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.UnitOfWork;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class SmokeAlarmDetectorConfigurationRepository : ISmokeAlarmDetectorConfigurationRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public SmokeAlarmDetectorConfigurationRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task AddDefaultSmokeAlarmConfigurationToDeviceAsync(Guid deviceId, IUnitOfWork? uow = null)
        {
            if (uow != null)
            {
                var ownedDb = (CamPortalDBContext)uow.Db;

                ownedDb.SmokeAlarmDetectorConfigurations.Add(new() { DeviceId = deviceId });

                return;
            }

            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            dbContext.SmokeAlarmDetectorConfigurations.Add(new() { DeviceId = deviceId });
            await dbContext.SaveChangesAsync();
        }

        public async Task<bool> UpdateConfigurationAsync(Guid deviceId, SmokeAlarmConfigurationDto configuration)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var result = await dbContext.SmokeAlarmDetectorConfigurations
                 .ExecuteUpdateAsync(x => x.SetProperty(y => y.MinBatterySOCForAlert, configuration.MinBatterySOCForAlert));

            return result > 0;
        }

        public async Task<SmokeAlarmConfigurationDto> GetSmokeAlarmConfigurationAsync(Guid deviceId)
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var configuration = await dbContext.SmokeAlarmDetectorConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.DeviceId == deviceId);

            return _mapper.Map<SmokeAlarmConfigurationDto>(configuration);
        }

        public async Task<int> CountDeviceConfigurationsAsync()
        {
            var dbContext = await _dbContextFactory.CreateDbContextAsync();

            return await dbContext.SmokeAlarmDetectorConfigurations.CountAsync();
        }
    }
}
