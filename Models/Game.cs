namespace BlueLock.Models;
using MongoDB.Bson;

public class Game
{
	public ObjectId id { get; set; }
	public required string gameId { get; set; }
	public float duration { get; set; }
}