namespace Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;

public class TrafficShaperRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Interface { get; set; } = "";
    public int BandwidthUp { get; set; } = 1000; // Kbps
    public int BandwidthDown { get; set; } = 1000; // Kbps
    public string Scheduler { get; set; } = "fq_codel"; // fq_codel, hfsc, cbq, priq
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TrafficShaperRuleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Interface { get; set; } = "";
    public int BandwidthUp { get; set; } = 1000;
    public int BandwidthDown { get; set; } = 1000;
    public string Scheduler { get; set; } = "fq_codel";
    public string Description { get; set; } = "";
    public int Enabled { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
