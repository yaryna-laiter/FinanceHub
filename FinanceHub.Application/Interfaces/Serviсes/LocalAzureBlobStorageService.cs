using Microsoft.AspNetCore.Http;

namespace FinanceGub.Application.Interfaces.Serviсes;

public class LocalAzureBlobStorageService : IAzureBlobStorageService
{
    public Task<string> AddUserPhotoAsync(IFormFile file)
    {
        return Task.FromResult(file.FileName);
    }

    public Task<string> AddPostPhotoAsync(IFormFile file)
    {
        return Task.FromResult(file.FileName);
    }

    public Task<string> AddMainHubPhotoAsync(IFormFile file)
    {
        return Task.FromResult(file.FileName);
    }

    public Task<string> AddBackHubPhotoAsync(IFormFile file)
    {
        return Task.FromResult(file.FileName);
    }

    public Task DeletePhotoAsync(string imageUrl)
    {
        return Task.CompletedTask;
    }
}