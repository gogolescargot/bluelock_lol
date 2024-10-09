using BlueLock.Models;
using MongoDB.Driver;
public interface IDatabaseService
{
    Task<IMongoDatabase?> ConnectDatabaseAsync();
    IMongoCollection<T> GetCollection<T>(IMongoDatabase database, string collectionName);
    Task<bool> GameExistsAsync(IMongoCollection<Game> gameCollection, string gameId);
    Task InsertGame(IMongoCollection<Game> gameCollection, string gameId, float gameDuration);
    Task<Statistic> InsertPlayerStatistics(PlayerStatistic playerStat, string gameId, IMongoCollection<Statistic> statCollection, float gameDuration);
    Task UpdatePlayerStats(Statistic stat, IMongoCollection<Statistic> statCollection, IMongoCollection<Player> playerCollection);
    Task<List<Player>> GetAllPlayersAsync(IMongoDatabase database);
}