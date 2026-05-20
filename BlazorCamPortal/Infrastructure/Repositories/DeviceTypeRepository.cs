using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Enums;
using CamPortal.Infrastructure.Data;
using CamPortal.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CamPortal.Infrastructure.Repositories
{
    public class DeviceTypeRepository : IDeviceTypeRepository
    {
        private readonly IDbContextFactory<CamPortalDBContext> _dbContextFactory;
        private readonly IMapper _mapper;

        public DeviceTypeRepository(IDbContextFactory<CamPortalDBContext> dbContextFactory, IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _mapper = mapper;
        }

        public async Task<Guid> CreateTypeAsync(CreateDeviceTypeDto dto)
        {
            var entity = _mapper.Map<DeviceType>(dto);

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            db.DeviceTypes.Add(entity);

            await db.SaveChangesAsync();

            return entity.Id;
        }

        public async Task<bool> DeleteTypeAsync(Guid typeId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.DeviceTypes
                .Where(x => x.Id == typeId)
                .ExecuteDeleteAsync();

            return result != 0;
        }

        public async Task<List<DeviceTypeDto>> GetAllTypesAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.DeviceTypes
                .AsNoTracking()
                .Select(type => _mapper.Map<DeviceTypeDto>(type))
                .ToListAsync();

            return result;
        }

        public async Task<DeviceTypeDto?> GetByIdAsync(Guid typeId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var entity = await db.DeviceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == typeId);

            if (entity is null)
            {
                return null;
            }

            return _mapper.Map<DeviceTypeDto>(entity);
        }

        public async Task<bool> DoesExistByNameAsync(string name)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            return await db.DeviceTypes
                .AsNoTracking()
                .AnyAsync(x => x.Name == name);
        }

        public async Task<DeviceTypeCategories> GetDeviceCategoryAsync(Guid typeId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var category = await db.DeviceTypes
                .AsNoTracking()
                .Where(x => x.Id == typeId)
                .Select(x => x.DeviceVariant)
                .FirstOrDefaultAsync();

            return category;
        }

        public async Task<List<DeviceTypeDto>> GetAllTypesByNameAsync(string name)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var result = await db.DeviceTypes
                .AsNoTracking()
                .Where(x => x.Name.Contains(name))
                .Select(type => _mapper.Map<DeviceTypeDto>(type))
                .ToListAsync();

            return result;
        }
    }
}
