using AzureMcp.Services.Networking.Models;

namespace AzureMcp.Services.Networking.Interfaces;

public interface IPublicIpAddressService
{
    Task<IEnumerable<PublicIPAddressDto>> ListPublicIpAddressesAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<PublicIPAddressDto?> GetPublicIpAddressAsync(string subscriptionId, string resourceGroupName, string publicIpName);
    Task<PublicIPAddressDto> CreatePublicIpAddressAsync(string subscriptionId, string resourceGroupName, PublicIPAddressCreateRequest request);
    Task<bool> DeletePublicIpAddressAsync(string subscriptionId, string resourceGroupName, string publicIpName);
}