using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BlueLock.Models;
using Fraxiinus.Rofl.Extract.Data;
using Fraxiinus.Rofl.Extract.Data.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using Newtonsoft.Json.Converters;

namespace BlueLock.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private async Task<Statistic> insertStatistic(PlayerStatistic jsonPlayer, string gameId, IMongoCollection<Statistic> statCollection, float gameDuration)
    {
        Statistic stat = new Statistic
        {
            gameId = gameId,
            puuid = jsonPlayer.PUUID,
            name = jsonPlayer.NAME,
            champion = jsonPlayer.SKIN,
            role = jsonPlayer.INDIVIDUAL_POSITION,
            kill = jsonPlayer.Missions_ChampionsKilled,
            assist = jsonPlayer.ASSISTS,
            death = jsonPlayer.NUM_DEATHS,
            visionScore = jsonPlayer.VISION_SCORE
        };

        stat.csMin = (jsonPlayer.Missions_MinionsKilled + jsonPlayer.NEUTRAL_MINIONS_KILLED) / gameDuration;

        if (stat.death == 0)
            stat.kda = (stat.kill + stat.assist) / 1;
        else
            stat.kda = (stat.kill + stat.assist) / stat.death;

        if (jsonPlayer.WIN == "Win")
            stat.win = true;
        else
            stat.win = false;
        
        await statCollection.InsertOneAsync(stat);

        return stat;
    }

    private async Task updatePlayer(Statistic stat, IMongoCollection<Statistic> statCollection, IMongoCollection<Player> playerCollection)
    {
        var filterStat = Builders<Statistic>.Filter.Eq("puuid", stat.puuid);
        var filterPlayer = Builders<Player>.Filter.Eq("puuid", stat.puuid);
        var count = await playerCollection.CountDocumentsAsync(filterPlayer);

        if (count == 0)
        {
            Player player = new Player
            {
                puuid = stat.puuid,
                name = stat.name,
                mainRole = stat.role,
                mainChamp = stat.champion,
                kill = stat.kill,
                assist = stat.assist,
                death = stat.death,
                kda = stat.kda,
                visionScore = stat.visionScore,
                csMin = stat.csMin,
                game = 1
            };
            if (stat.win)
            {
                player.win = 1;
                player.lose = 0;
            }
            else
            {
                player.win = 0;
                player.lose = 1;
            }
            player.winRate = player.win / player.game;

            await playerCollection.InsertOneAsync(player);

        }

        else
        {
            List<Statistic> playerStats = await statCollection.Find(filterStat).ToListAsync();

            var AllStatsPlayer = await playerCollection.Find(Builders<Player>.Filter.Eq("puuid", stat.puuid)).FirstOrDefaultAsync();

            AllStatsPlayer.kill = 0;
            AllStatsPlayer.assist = 0;
            AllStatsPlayer.death = 0;
            AllStatsPlayer.kda = 0;
            AllStatsPlayer.game = 0;
            AllStatsPlayer.visionScore = 0;
            AllStatsPlayer.csMin = 0;
            AllStatsPlayer.win = 0;
            AllStatsPlayer.lose = 0;
            AllStatsPlayer.winRate = 0;
            AllStatsPlayer.kda = 0;


            foreach (Statistic stats in playerStats)
            {
                AllStatsPlayer.kill += stats.kill;
                AllStatsPlayer.assist += stats.assist;
                AllStatsPlayer.death += stats.death;
                AllStatsPlayer.kda += stats.kda;
                AllStatsPlayer.game += 1;
                AllStatsPlayer.visionScore += stats.visionScore;
                AllStatsPlayer.csMin += stats.csMin;
                if (stats.win)
                {
                    AllStatsPlayer.win += 1;
                }
                else
                {
                    AllStatsPlayer.lose += 1;
                }
            }

            AllStatsPlayer.winRate = (float)AllStatsPlayer.win / AllStatsPlayer.game;
            AllStatsPlayer.visionScore /= AllStatsPlayer.game;
            AllStatsPlayer.csMin /= AllStatsPlayer.game;
            AllStatsPlayer.kda /= AllStatsPlayer.game;

            await playerCollection.ReplaceOneAsync(Builders<Player>.Filter.Eq("puuid", AllStatsPlayer.puuid), AllStatsPlayer);
        }
    }

    private async Task insertGame(IMongoCollection<Game> gameCollection, string gameIdTime, float gameDuration)
    {

        Game game = new Game()
        {
            gameId = gameIdTime,
            duration = gameDuration
        };

        await gameCollection.InsertOneAsync(game);
    }

    private async Task<IMongoDatabase?> connectDatabase()
    {
        string dbUser = _configuration.GetValue<string>("ConnectionStrings:DB_USER") ?? string.Empty;
        string dbPass = _configuration.GetValue<string>("ConnectionStrings:DB_PASS") ?? string.Empty;

        var connectionUri = $"mongodb+srv://{dbUser}:{dbPass}@bluelock.x50ei.mongodb.net/?retryWrites=true&w=majority&appName=BlueLock";

        var settings = MongoClientSettings.FromConnectionString(connectionUri);

        settings.ServerApi = new ServerApi(ServerApiVersion.V1);

        var client = new MongoClient(settings);

        var database = client.GetDatabase("bluelock");

        try
        {
            var result = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            _logger.LogInformation("Pinged your deployment. You successfully connected to MongoDB!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return null;
        }

        return database;
    }

    private async Task<int> downloadRoflFile(IFormFile file)
    {
        if (file == null || file.Length == 0 || Path.GetExtension(file.FileName) != ".rofl")
        {
            return 1;
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/rofl", file.FileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        _logger.LogInformation("Game Uploaded");

        return 0;
    }

    private async Task<string?> RoflToJson(IFormFile file)
    {
        var options = new ReplayReaderOptions();

        var demo = Path.GetFileNameWithoutExtension(file.FileName);
    
        var roflPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/rofl", demo + ".rofl");

        var jsonPath =  Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/json", demo + ".json");

        var replay = await ReplayReader.ReadReplayAsync(roflPath, options);
    
        if (replay.Result is not ROFL2 rofl2 || rofl2.Metadata == null)
        {
            return null;
        }

        _logger.LogInformation("Game Version: {GameVersion}", rofl2.Metadata.GameVersion);
        await rofl2.ToJsonFile(new FileInfo(roflPath), jsonPath);

        return jsonPath;
    }

    private async Task<Root?> deserializeJson(string jsonPath)
    {
        return await Task.Run(() =>
        {
            using (StreamReader r = new StreamReader(jsonPath))
            {
                string json = r.ReadToEnd();
                Root? rootObject = JsonConvert.DeserializeObject<Root>(json);
                if (rootObject == null)
                {
                    return null;
                }
                return rootObject;
            }
        });
    }


    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (await downloadRoflFile(file) == 1)
        {
            _logger.LogError("Problem with file.");
            return BadRequest("Problem with file.");
        }

        var jsonPath = await RoflToJson(file);

        if (jsonPath == null)
        {
            _logger.LogError("Failed to read replay or metadata is null.");
            return BadRequest("Failed to read replay or metadata is null.");
        }

        IMongoDatabase? database = await connectDatabase();

        if (database == null)
        {
            _logger.LogError("Failed to connect to the database.");
            return BadRequest("Failed to connect to the database.");
        }

        Root? rootObject = await deserializeJson(jsonPath);

        if (rootObject == null)
        {
            _logger.LogError("Failed to deserialize JSON.");
            return BadRequest("Failed to deserialize JSON.");
        }

        float gameDuration = (float)rootObject.Metadata.GameLength / 60000;

        string gameIdTime = rootObject.Metadata.GameLength.ToString() + rootObject.Metadata.LastGameChunkId + rootObject.Metadata.LastKeyframeId;

        var statCollection = database.GetCollection<Statistic>("stat");

        var playerCollection = database.GetCollection<Player>("player");

        var gameCollection = database.GetCollection<Game>("game");

        var filterGame = Builders<Game>.Filter.Eq("gameId", gameIdTime);
        var count = await gameCollection.CountDocumentsAsync(filterGame);

        if (count != 0)
        {
            _logger.LogError("Game already uploaded.");
            return BadRequest("Game already uploaded.");
        }

        foreach (var jsonPlayer in rootObject.Metadata.PlayerStatistics)
        {
            Statistic player = await insertStatistic(jsonPlayer, gameIdTime, statCollection, gameDuration);

            await updatePlayer(player, statCollection, playerCollection);
        }

        await insertGame(gameCollection, gameIdTime, gameDuration);

        return RedirectToAction("Index");
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}
