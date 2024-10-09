namespace BlueLock.Models;
using MongoDB.Bson;

public class Statistic
{
	public ObjectId id { get; set; }
	public required string gameId { get; set; }
	public required string puuid { get; set; }
    public required string name { get; set; }
	public required string champion { get; set; }
	public required string role { get; set; }
	public int kill { get; set; }
	public int assist { get; set; }
	public int death { get; set; }
	public float kda { get; set; }
	public float csMin { get; set; }
	public float visionScore { get; set; }
	public bool win { get; set; }
}