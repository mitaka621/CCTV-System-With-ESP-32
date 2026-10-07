using AutoMapper;
using CamPortal.Contracts.Abstractions.Repositories;
using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Dtos.DeviceTypeDtos;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace CamPortal.Core.Services.Devices
{
    public class DeviceTypeService : IDeviceTypeService
    {
        private readonly IDeviceTypeRepository _deviceTypeRepository;
        private readonly IDeviceTypeIconStorageService _deviceTypeIconStorageService;
        private readonly IMapper _mapper;

        private readonly string _defaultIconName;

        public DeviceTypeService(
            IDeviceTypeRepository deviceTypeRepository,
            IDeviceTypeIconStorageService deviceTypeIconStorageService,
            IMapper mapper,
            IConfiguration configuration)
        {
            _deviceTypeRepository = deviceTypeRepository;
            _deviceTypeIconStorageService = deviceTypeIconStorageService;
            _mapper = mapper;

            _defaultIconName = configuration.GetSection("DeviceTypeIconsConfig")["DefaultIconName"]
                ?? throw new ArgumentNullException("DeviceTypeIconsConfig:DefaultIconName not configured");
        }

        public async Task<List<DeviceTypeDisplayModel>> GetAllDeviceTypesAsync()
        {
            var deviceTypes = await _deviceTypeRepository.GetAllTypesAsync();

            return deviceTypes
                .Select(dto =>
                {
                    var model = _mapper.Map<DeviceTypeDisplayModel>(dto);
                    return model;
                })
                .ToList();
        }

        public async Task<Guid> CreateDeviceTypeAsync(CreateDeviceTypeModel model, CancellationToken ct)
        {
            if (!MiscUtilities.ValidateModel(model, out ICollection<ValidationResult> validationResults))
            {
                throw new ArgumentException(string.Join(", ", validationResults.Where(x => !string.IsNullOrEmpty(x.ErrorMessage)).Select(v => v.ErrorMessage)));
            }

            if (await _deviceTypeRepository.DoesExistByNameAsync(model.Name))
            {
                throw new InvalidOperationException($"A device type named '{model.Name}' already exists.");
            }

            var iconName = model.IconFile == null ? _defaultIconName : await _deviceTypeIconStorageService.SaveAsync(model.IconFile, ct);

            var dto = new CreateDeviceTypeDto
            {
                Name = model.Name,
                IconName = iconName,
                IconUpdatedAt = DateTime.UtcNow,
                DeviceCategory = model.DeviceCategory,
                Description = model.Description,
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

        public async Task<bool> UpdateDeviceTypeAsync(Guid deviceTypeId, CreateDeviceTypeModel model, CancellationToken ct)
        {
            if (!MiscUtilities.ValidateModel(model, out ICollection<ValidationResult> validationResults))
            {
                throw new ArgumentException(string.Join(", ", validationResults.Where(x => !string.IsNullOrEmpty(x.ErrorMessage)).Select(v => v.ErrorMessage)));
            }

            var existing = await _deviceTypeRepository.GetByIdAsync(deviceTypeId);
            if (existing is null)
            {
                return false;
            }

            if (await _deviceTypeRepository.DoesExistByNameAsync(model.Name, deviceTypeId))
            {
                throw new InvalidOperationException($"A device type named '{model.Name}' already exists.");
            }

            if (existing.DeviceCategory != model.DeviceCategory && await _deviceTypeRepository.IsTypeInUseAsync(deviceTypeId))
            {
                throw new InvalidOperationException("The category cannot be changed while devices are using this device type.");
            }

            var isIconReplaced = model.IconFile != null;
            var iconName = isIconReplaced ? await _deviceTypeIconStorageService.SaveAsync(model.IconFile!, ct) : existing.IconName;

            var dto = new UpdateDeviceTypeDto
            {
                Id = deviceTypeId,
                Name = model.Name,
                IconName = iconName,
                IconUpdatedAt = isIconReplaced ? DateTime.UtcNow : existing.IconUpdatedAt,
                DeviceCategory = model.DeviceCategory,
                Description = model.Description,
            };

            bool updated;

            try
            {
                updated = await _deviceTypeRepository.UpdateTypeAsync(dto);
            }
            catch
            {
                if (isIconReplaced)
                {
                    await _deviceTypeIconStorageService.DeleteAsync(iconName);
                }

                throw;
            }

            if (!isIconReplaced)
            {
                return updated;
            }

            if (!updated)
            {
                await _deviceTypeIconStorageService.DeleteAsync(iconName);
                return false;
            }

            if (string.Compare(existing.IconName, _defaultIconName, true) != 0)
            {
                await _deviceTypeIconStorageService.DeleteAsync(existing.IconName);
            }

            return true;
        }

        public async Task<bool> DeleteDeviceTypeAsync(Guid deviceTypeId)
        {
            var dto = await _deviceTypeRepository.GetByIdAsync(deviceTypeId);
            if (dto is null)
            {
                return false;
            }

            var deleted = await _deviceTypeRepository.DeleteTypeAsync(deviceTypeId);
            if (deleted && string.Compare(dto.IconName, _defaultIconName, true) != 0)
            {
                await _deviceTypeIconStorageService.DeleteAsync(dto.IconName);
            }

            return deleted;
        }

        public Task<bool> DoesDeviceTypeExistByNameAsync(string name, Guid? excludedDeviceTypeId = null)
        {
            return _deviceTypeRepository.DoesExistByNameAsync(name, excludedDeviceTypeId);
        }

        public Task<bool> IsDeviceTypeInUseAsync(Guid deviceTypeId)
        {
            return _deviceTypeRepository.IsTypeInUseAsync(deviceTypeId);
        }

        public async Task<List<DeviceTypeDisplayModel>> GetDevicesByNameAsync(string name)
        {
            var deviceTypes = await _deviceTypeRepository.GetAllTypesByNameAsync(name);

            return deviceTypes
                .Select(dto =>
                {
                    var model = _mapper.Map<DeviceTypeDisplayModel>(dto);
                    return model;
                })
                .ToList();
        }
    }
}
