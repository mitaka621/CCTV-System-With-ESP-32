using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Dtos.SmokeAlarmDetectorDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Contracts.Models;
using CamPortal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories.PerDeviceRepository
{
    public class SmokeAlarmRepository : DeviceRepository, ISmokeAlarmRepository
    {

        public SmokeAlarmRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper) : base(dbContextFactory, mapper) { }

        public async Task<List<SmokeAlarmDto>> GetAllSmokeAlarmsWithTypeAndLatestTelemetryAsync(DeviceFilterModel filterModel)
        {
            var db = await DbContextFactory.CreateDbContextAsync();

            var query = GetDeviceBaseFilterQuery(db, filterModel)
                .Where(x => x.DeviceType.DeviceCategory == DeviceTypeCategories.SmokeAlarm)
                .Select(x => new SmokeAlarmDto()
                {
                    Id = x.Id,
                    Fingerprint = x.Fingerprint,
                    PairStatus = x.PairStatus,
                    PublicKey = x.PublicKey,
                    CreatedAt = x.CreatedAt,
                    Ipv4Address = x.Ipv4Address,
                    Name = x.Name,
                    UpdatedAt = x.UpdatedAt,
                    DeviceType = Mapper.Map<DeviceTypeDto>(x.DeviceType),
                    LatestTelemetry = x.SmokeAlarmTelemetries!.OrderByDescending(t => t.LoggedTimeUTC).Select(t => Mapper.Map<SmokeAlarmShortPayloadDto>(t)).FirstOrDefault()
                        ?? new()
                });

            return await query.ToListAsync();
        }
    }
}
