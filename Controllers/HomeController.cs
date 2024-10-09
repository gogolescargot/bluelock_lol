using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BlueLock.Models;
using MongoDB.Driver;

namespace BlueLock.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDatabaseService _databaseService;
    private readonly IRoflService _roflService;
    
    public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IDatabaseService databaseService, IRoflService roflService)
    {
        _logger = logger;
        _configuration = configuration;
        _databaseService = databaseService;
        _roflService = roflService;
    }

    public async Task<IActionResult> Index()
    {
        IMongoDatabase? database = await _databaseService.ConnectDatabaseAsync();

        if (database == null)
        {
            _logger.LogError("Failed to connect to the database.");
            return BadRequest("Failed to connect to the database.");
        }
        var playerCollection = database.GetCollection<Player>("player");

        List<Player> players = await playerCollection.Find(_ => true).ToListAsync();

        return View(players);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {        
        if (await _roflService.DownloadRoflFile(file) == 1)
        {
            _logger.LogError("Invalid file upload.");
            return BadRequest("Invalid file.");
        }

        var jsonPath = await _roflService.ConvertRoflToJson(file);

        if (jsonPath == null)
        {
            _logger.LogError("Failed to convert ROFL to JSON.");
            return BadRequest("Failed to process replay file.");
        }

        var database = await _databaseService.ConnectDatabaseAsync();

        if (database == null)
        {
            _logger.LogError("Failed to connect to MongoDB.");
            return StatusCode(500, "Database connection error.");
        }

        var rootObject = await _roflService.DeserializeJson(jsonPath);
        if (rootObject == null)
        {
            _logger.LogError("JSON deserialization failed.");
            return BadRequest("Invalid JSON data.");
        }

        string gameId = _roflService.GenerateGameId(rootObject);

        var gameCollection = _databaseService.GetCollection<Game>(database, "game");

        if (await _databaseService.GameExistsAsync(gameCollection, gameId))
        {
            _logger.LogError("Game already exists.");
            return BadRequest("Game already uploaded.");
        }

        var statCollection = _databaseService.GetCollection<Statistic>(database, "stat");
        var playerCollection = _databaseService.GetCollection<Player>(database, "player");

        foreach (var playerStat in rootObject.Metadata.PlayerStatistics)
        {
            var playerStatRecord = await _databaseService.InsertPlayerStatistics(playerStat, gameId, statCollection, rootObject.Metadata.GameLength / 60000.0f);
            await _databaseService.UpdatePlayerStats(playerStatRecord, statCollection, playerCollection);
        }

        await _databaseService.InsertGame(gameCollection, gameId, rootObject.Metadata.GameLength / 60000.0f);

        return RedirectToAction("Index");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}
