namespace Monolith.FireWall.Platform.Models;

public sealed class PingRequest
{
    public string? Host { get; set; }
    public int? Count { get; set; }
    public int? Size { get; set; }
    public int? IntervalMs { get; set; }
    public int? TimeoutMs { get; set; }
}

public sealed class TracerouteRequest
{
    public string? Host { get; set; }
    public int? MaxHops { get; set; }
    public int? WaitMs { get; set; }
    public bool Fast { get; set; } = false;
    public bool Resolve { get; set; } = false;
}

public sealed class MtrRequest
{
    public string? Host { get; set; }
    public int? Count { get; set; }
    public int? IntervalMs { get; set; }
    public bool Resolve { get; set; } = false;
}

public sealed class PingResult
{
    public string Host { get; set; } = string.Empty;
    public int Transmitted { get; set; }
    public int Received { get; set; }
    public double LossPercent { get; set; }
    public double? MinMs { get; set; }
    public double? AvgMs { get; set; }
    public double? MaxMs { get; set; }
    public double? MdevMs { get; set; }
    public string[] OutputLines { get; set; } = Array.Empty<string>();
}

public sealed class TracerouteHop
{
    public int Hop { get; set; }
    public string Host { get; set; } = string.Empty;
    public List<double> TimesMs { get; set; } = new();
    public string Raw { get; set; } = string.Empty;
}

public sealed class TracerouteResult
{
    public string Host { get; set; } = string.Empty;
    public List<TracerouteHop> Hops { get; set; } = new();
    public string[] OutputLines { get; set; } = Array.Empty<string>();
}

public sealed class MtrHop
{
    public int Hop { get; set; }
    public string Host { get; set; } = string.Empty;
    public double LossPercent { get; set; }
    public int Sent { get; set; }
    public double LastMs { get; set; }
    public double AvgMs { get; set; }
    public double BestMs { get; set; }
    public double WorstMs { get; set; }
    public double StDevMs { get; set; }
    public string Raw { get; set; } = string.Empty;
}

public sealed class MtrResult
{
    public string Host { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<MtrHop> Hops { get; set; } = new();
    public string[] OutputLines { get; set; } = Array.Empty<string>();
}
