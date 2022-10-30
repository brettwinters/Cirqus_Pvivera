namespace d60.Cirqus.Numbers;

public interface IGlobalSequenceNumberService
{
	/// <summary>
	/// Returns guaranteed unique ticks for the Utc
	/// </summary>
	long NewGlobalSequenceNumber { get; }
}