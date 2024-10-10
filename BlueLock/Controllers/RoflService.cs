using BlueLock.Models;
using Fraxiinus.Rofl.Extract.Data;
using Fraxiinus.Rofl.Extract.Data.Models;
using Newtonsoft.Json;

namespace BlueLock.Services
{
    public class RoflService : IRoflService
    {
        private readonly ILogger<RoflService> _logger;

        public RoflService(ILogger<RoflService> logger)
        {
            _logger = logger;
        }

        public async Task<int> DownloadRoflFile(IFormFile file)
        {
            if (file == null || file.Length == 0 || Path.GetExtension(file.FileName) != ".rofl")
            {
                _logger.LogError("Invalid file upload attempt.");
                return 1;
            }

            var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/rofl");

            Directory.CreateDirectory(uploadsDirectory);

            var filePath = Path.Combine(uploadsDirectory, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation($"ROFL file {file.FileName} uploaded successfully.");
            return 0;
        }

        public async Task<string?> ConvertRoflToJson(IFormFile file)
        {
            var demo = Path.GetFileNameWithoutExtension(file.FileName);
            var roflPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/rofl", demo + ".rofl");
            var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/json", demo + ".json");

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/json"));

            //try
            //{
            //    using (var stream = new FileStream(roflPath, FileMode.Create))
            //    {
            //        ReplayType checkRofl = await ReplayReader.DetectReplayTypeAsync(stream);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Invalid rofl file.");
            //    return null;
            //}

            var options = new ReplayReaderOptions();
            var replay = await ReplayReader.ReadReplayAsync(roflPath, options);

            if (replay.Result is not ROFL2 rofl2 || rofl2.Metadata == null)
            {
                _logger.LogError($"Failed to extract metadata from ROFL file: {file.FileName}");
                return null;
            }

            _logger.LogInformation($"Game Version: {rofl2.Metadata.GameVersion}");

            await rofl2.ToJsonFile(new FileInfo(roflPath), jsonPath);
            _logger.LogInformation($"ROFL file {file.FileName} successfully converted to JSON.");
            return jsonPath;
        }

        public async Task<Root?> DeserializeJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                _logger.LogError($"JSON file not found: {jsonPath}");
                return null;
            }

            try
            {
                using (StreamReader reader = new StreamReader(jsonPath))
                {
                    string jsonContent = await reader.ReadToEndAsync();
                    var rootObject = JsonConvert.DeserializeObject<Root>(jsonContent);

                    if (rootObject == null)
                    {
                        _logger.LogError("Deserialization failed, JSON content is null.");
                        return null;
                    }

                    _logger.LogInformation("JSON deserialized successfully.");
                    return rootObject;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON file.");
                return null;
            }
        }

        public string GenerateGameId(Root rootObject)
        {
            var metadata = rootObject.Metadata;
            string gameId = $"{metadata.GameLength}{metadata.LastGameChunkId}{metadata.LastKeyframeId}";
            _logger.LogInformation($"Generated Game ID: {gameId}");
            return gameId;
        }
    }
}
