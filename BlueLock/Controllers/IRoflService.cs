using BlueLock.Models;
public interface IRoflService
{
    Task<int> DownloadRoflFile(IFormFile file);
    Task<string?> ConvertRoflToJson(IFormFile file);
    Task<Root?> DeserializeJson(string jsonPath);
    string GenerateGameId(Root rootObject);
}