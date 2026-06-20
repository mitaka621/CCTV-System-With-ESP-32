using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Models;

namespace CamPortal.Core.Services.Devices
{
    public class DeviceTypeService : IDeviceTypeService
    {
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IDeviceTypeIconStorageService _deviceTypeIconStorageService;
        private readonly IMapper _mapper;

        public DeviceTypeService(
            IDeviceTypeRepository deviceTypeRepository,
            IDeviceTypeIconStorageService deviceTypeIconStorageService,
            IMapper mapper)
        {
            _deviceTypeRepository = deviceTypeRepository;
            _deviceTypeIconStorageService = deviceTypeIconStorageService;
            _mapper = mapper;
        }

        public async Task<List<DeviceTypeDisplayModel>> GetAllDeviceTypesAsync()
        {
            var deviceTypes = await _deviceTypeRepository.GetAllTypesAsync();

            return deviceTypes
                .Select(dto =>
                {
                    var model = _mapper.Map<DeviceTypeDisplayModel>(dto);
                    model.IconUrl = _deviceTypeIconStorageService.BuildPublicUrl(model.IconName, model.IconUpdatedAt);
                    return model;
                })
                .ToList();
        }

        public async Task<Guid> CreateDeviceTypeAsync(CreateDeviceTypeModel model, CancellationToken ct)
        {
            if (model.IconFile is null)
            {
                throw new InvalidOperationException("Icon file is required.");
            }

            if (await _deviceTypeRepository.DoesExistByNameAsync(model.Name))
            {
                throw new InvalidOperationException($"A device type named '{model.Name}' already exists.");
            }

            var iconName = await _deviceTypeIconStorageService.SaveAsync(model.IconFile, ct);

            var dto = new CreateDeviceTypeDto
            {
                Name = model.Name,
                IconName = iconName,
                IconUpdatedAt = DateTime.UtcNow,
                DeviceVariant = model.DeviceVariant,
            };

            try
            {
                return await _deviceTypeRepository.CreateTypeAsync(dto);
            }
            catch
            {
                await _deviceTypeIconStorageService.DeleteAsync(iconName);
                throw;
            }
        }

        public async Task<bool> DeleteDeviceTypeAsync(Guid deviceTypeId)
        {
            var dto = await _deviceTypeRepository.GetByIdAsync(deviceTypeId);
            if (dto is null)
            {
                return false;
            }

            var deleted = await _deviceTypeRepository.DeleteTypeAsync(deviceTypeId);
            if (deleted)
            {
                await _deviceTypeIconStorageService.DeleteAsync(dto.IconName);
            }

            return deleted;
        }

        public Task<bool> DoesDeviceTypeExistByNameAsync(string name)
        {
            return _deviceTypeRepository.DoesExistByNameAsync(name);
        }

        public async Task<List<DeviceTypeDisplayModel>> GetDevicesByNameAsync(string name)
        {
            var deviceTypes = await _deviceTypeRepository.GetAllTypesByNameAsync(name);

            return deviceTypes
                .Select(dto =>
                {
                    var model = _mapper.Map<DeviceTypeDisplayModel>(dto);
                    model.IconUrl = _deviceTypeIconStorageService.BuildPublicUrl(model.IconName, model.IconUpdatedAt);
                    return model;
                })
                .ToList();
        }
    }
}
