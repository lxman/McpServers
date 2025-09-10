using TrafficGenerator.Models;

namespace TrafficGenerator.Services;

public interface ITrafficGenerationService
{
    Task<TrafficGenerationResponse> GenerateReconnaissanceTraffic(ReconnaissanceRequest request);
    Task<TrafficGenerationResponse> GenerateMitmTraffic(MitmRequest request);
    Task<TrafficGenerationResponse> GenerateExfiltrationTraffic(ExfiltrationRequest request);
    Task<TrafficGenerationResponse> GenerateC2Traffic(C2CommunicationRequest request);
    Task<IEnumerable<TrafficGenerationResponse>> GetActiveSessionsAsync();
    Task<TrafficGenerationResponse?> GetSessionAsync(string sessionId);
    Task<bool> StopSessionAsync(string sessionId);
    Task<IEnumerable<object>> GetAvailableInterfacesAsync();
}
