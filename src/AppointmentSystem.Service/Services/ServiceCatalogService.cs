using AppointmentSystem.Data.Repositories.Interfaces;
using AppointmentSystem.Bll.Services.Interfaces;
using Service = AppointmentSystem.Data.Entities.Service;

namespace AppointmentSystem.Bll.Services;

public class ServiceCatalogService : IServiceCatalogService
{
    private readonly IServiceRepository _repo;
    public ServiceCatalogService(IServiceRepository repo) => _repo = repo;

    public Task<IEnumerable<Service>> GetAllAsync(bool includeInactive = false) => _repo.GetAllAsync(includeInactive);
    public Task<Service?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
    public Task<int> CreateAsync(Service s) => _repo.CreateAsync(s);
    public Task UpdateAsync(Service s) => _repo.UpdateAsync(s);
    public Task SetActiveAsync(int id, bool active) => _repo.SetActiveAsync(id, active);
    public Task<IEnumerable<Service>> GetForProviderAsync(int providerId) => _repo.GetForProviderAsync(providerId);
    public Task SetProviderServicesAsync(int providerId, IEnumerable<int> ids) => _repo.SetProviderServicesAsync(providerId, ids);
    public Task<IEnumerable<int>> GetProviderServiceIdsAsync(int providerId) => _repo.GetProviderServiceIdsAsync(providerId);
}
