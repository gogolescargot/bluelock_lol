namespace BlueLock.Models;

public class Metadata
{
	public required int GameLength { get; set; }
	public required int LastGameChunkId { get; set; }
	public required int LastKeyframeId { get; set; }
	public required List<PlayerStatistic> PlayerStatistics { get; set; }
}