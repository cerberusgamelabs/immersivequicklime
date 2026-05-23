using System.Collections.Generic;

namespace ImmersiveQuicklime.code;

public sealed class LimestoneKilnConfig
{
    public int MaxDimension { get; set; } = 7;
    public int MaxHeight { get; set; } = 1;
    public double WarmupHours { get; set; } = 0.5;
    public double HoursPerPair { get; set; } = 0.5;
    public double BreakChance { get; set; } = 0.10;
    public int InputUnits { get; set; } = 2;
    public int OutputUnits { get; set; } = 3;
    public List<LimestoneFuelConfig> Fuels { get; set; } = new();
}

public sealed class LimestoneFuelConfig
{
    public string Code { get; set; } = "";
    public string Type { get; set; } = "item";
    public double BurnHours { get; set; } = 1;
}
