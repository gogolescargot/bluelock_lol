using BlueLock.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueLock.Database;
public class DatabaseService : IDatabaseService
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<DatabaseService> _logger;

	public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
	{
		_configuration = configuration;
		_logger = logger;
	}

	public async Task<IMongoDatabase?> ConnectDatabaseAsync()
	{
		string? dbUser = _configuration["ConnectionStrings:DB_USER"];
		string? dbPass = _configuration["ConnectionStrings:DB_PASS"];
		var connectionUri = $"mongodb+srv://{dbUser}:{dbPass}@bluelock.x50ei.mongodb.net/?retryWrites=true&w=majority&appName=BlueLock";

		var settings = MongoClientSettings.FromConnectionString(connectionUri);
		settings.ServerApi = new ServerApi(ServerApiVersion.V1);
		var client = new MongoClient(settings);

		try
		{
			var result = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
			_logger.LogInformation("Successfully connected to MongoDB.");
			return client.GetDatabase("bluelock");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to connect to MongoDB.");
			return null;
		}
	}

	public IMongoCollection<T> GetCollection<T>(IMongoDatabase database, string collectionName) =>
		database.GetCollection<T>(collectionName);

	public async Task<bool> GameExistsAsync(IMongoCollection<Game> gameCollection, string gameId)
	{
		var filter = Builders<Game>.Filter.Eq(g => g.gameId, gameId);
		var count = await gameCollection.CountDocumentsAsync(filter);
		return count > 0;
	}

	public async Task InsertGame(IMongoCollection<Game> gameCollection, string gameId, float gameDuration)
	{
		var game = new Game { gameId = gameId, duration = gameDuration };
		await gameCollection.InsertOneAsync(game);
	}

	public async Task<Statistic> InsertPlayerStatistics(PlayerStatistic playerStat, string gameId, IMongoCollection<Statistic> statCollection, float gameDuration)
	{
		var stat = new Statistic
		{
			gameId = gameId,
			puuid = playerStat.PUUID,
			name = playerStat.NAME,
			champion = playerStat.SKIN,
			role = playerStat.INDIVIDUAL_POSITION,
			kill = playerStat.Missions_ChampionsKilled,
			assist = playerStat.ASSISTS,
			death = playerStat.NUM_DEATHS,
			visionScore = playerStat.VISION_SCORE,
			csMin = (playerStat.Missions_MinionsKilled + playerStat.NEUTRAL_MINIONS_KILLED) / gameDuration,
			kda = playerStat.NUM_DEATHS == 0 ? (playerStat.Missions_ChampionsKilled + playerStat.ASSISTS) / 1.0f :
												(playerStat.Missions_ChampionsKilled + playerStat.ASSISTS) / playerStat.NUM_DEATHS,
			win = playerStat.WIN == "Win"
		};

		await statCollection.InsertOneAsync(stat);
		return stat;
	}

	public async Task UpdatePlayerStats(Statistic stat, IMongoCollection<Statistic> statCollection, IMongoCollection<Player> playerCollection)
	{
		var playerFilter = Builders<Player>.Filter.Eq(p => p.puuid, stat.puuid);
		var existingPlayer = await playerCollection.Find(playerFilter).FirstOrDefaultAsync();

		if (existingPlayer == null)
		{
			var newPlayer = new Player
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
				game = 1,
				win = stat.win ? 1 : 0,
				lose = stat.win ? 0 : 1,
				winRate = stat.win ? 1 : 0
			};

			await playerCollection.InsertOneAsync(newPlayer);
		}
		else
		{
			var update = Builders<Player>.Update
			.Inc(p => p.kill, stat.kill)
			.Inc(p => p.assist, stat.assist)
			.Inc(p => p.death, stat.death)
			.Set(p => p.visionScore, (existingPlayer.visionScore * existingPlayer.game + stat.visionScore) / (existingPlayer.game + 1))
			.Set(p => p.csMin, (existingPlayer.csMin * existingPlayer.game + stat.csMin) / (existingPlayer.game + 1))
			.Inc(p => p.win, stat.win ? 1 : 0)
			.Inc(p => p.lose, stat.win ? 0 : 1)
			.Set(p => p.kda, (existingPlayer.kda * existingPlayer.game + stat.kda) / (existingPlayer.game + 1))
			.Set(p => p.winRate, (existingPlayer.win + (stat.win ? 1 : 0)) / (float)(existingPlayer.game + 1))
			.Inc(p => p.game, 1);

			await playerCollection.UpdateOneAsync(playerFilter, update);
		}
	}

	public async Task<List<Player>> GetAllPlayersAsync(IMongoDatabase database)
	{
		var playerCollection = GetCollection<Player>(database, "player");
		return await playerCollection.Find(FilterDefinition<Player>.Empty).ToListAsync();
	}
}
