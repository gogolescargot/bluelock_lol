namespace BlueLock.Models;
using MongoDB.Bson;

public class Player
{
	public ObjectId id { get; set; }
    public required string puuid { get; set; }
    public required string name { get; set; }
    public string mainRole { get; set; }
    public string mainChamp { get; set; }
	public int kill { get; set; }
	public int assist { get; set; }
	public int death { get; set; }
	public float kda { get; set; }
	public float visionScore { get; set; }
	public float csMin { get; set; }
	public int game { get; set; }
	public float winRate { get; set; }
	public int win { get; set; }
	public int lose { get; set; }
}