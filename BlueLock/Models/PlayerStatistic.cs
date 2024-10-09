namespace BlueLock.Models;

public class PlayerStatistic
{
	public required int Missions_ChampionsKilled { get; set; }
	public required int Missions_MinionsKilled { get; set; }
	public required int ASSISTS { get; set; }
	public required string INDIVIDUAL_POSITION { get; set; }
	public required string NAME { get; set; }
	public required int NEUTRAL_MINIONS_KILLED { get; set; }
	public required int NUM_DEATHS { get; set; }
	public required string PUUID { get; set; }
	public required string SKIN { get; set; }
	public required float VISION_SCORE { get; set; }
	public required string WIN { get; set; }
}