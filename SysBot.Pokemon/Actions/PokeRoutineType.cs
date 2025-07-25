namespace SysBot.Pokemon;

/// <summary>
/// Type of routine the Bot carries out.
/// </summary>
public enum PokeRoutineType
{
    /// <summary> Sits idle waiting to be re-tasked. </summary>
    Idle = 0,

    /// <summary> Performs random trades using a predetermined pool of data. </summary>
    SurpriseTrade = 1,

    /// <summary> Performs the behavior of all trade bots. </summary>
    FlexTrade = 2,
    /// <summary> Performs only P2P Link Trades of specific data. </summary>
    LinkTrade = 3,
    /// <summary> Performs a seed check without transferring data from the bot. </summary>
    SeedCheck = 4,
    /// <summary> Performs a clone operation on the partner's data, sending them a copy of what they show. </summary>
    Clone = 5,
    /// <summary> Exports files for all data shown to the bot. </summary>
    Dump = 6,

    /// <summary> Performs group battles as a host. </summary>
    RaidBot = 7,

    /// <summary> Performs group battles as a host. </summary>
    MaxLair = 8,

    /// <summary> Triggers walking encounters until the criteria is satisfied. </summary>
    EncounterLine = 1_000,

    /// <summary> Triggers reset encounters until the criteria is satisfied. </summary>
    Reset = 1_001,

    /// <summary> Triggers encounters with Sword &amp; Shield box legend until the criteria is satisfied. </summary>
    DogBot = 1_002,

    /// <summary> Retrieves eggs from the Day Care. </summary>
    EggFetch = 1_003,

    /// <summary> Revives fossils until the criteria is satisfied. </summary>
    FossilBot = 1_004,

    /// <summary> Similar to idle, but identifies the bot as available for Remote input (Twitch Plays, etc). </summary>
    RemoteControl = 6_000,

    // Add your own custom bots here so they don't clash for future main-branch bot releases.

    /// <summary> Catches Ruinous until the criteria is satisfied, (optionally) using 100% catch cheat.  </summary>
    EncounterRuinous = 7_000,

    /// <summary> Catches Gimmighoul until the criteria is satisfied, (optionally) using 100% catch cheat.  </summary>
    EncounterGimmighoul = 7_001,

    /// <summary> Catches Royal three until the criteria is satisfied, (optionally) using 100% catch cheat.  </summary>
    EncounterLoyal = 7_002,

    /// <summary> Searches the overworld until the criteria is satisfied.  </summary>
    EncounterOverworld = 7_003,

    /// <summary> Catches Royal three until the criteria is satisfied, (optionally) using 100% catch cheat.  </summary>
    EncounterParadox = 7_004,

    /// <summary> Catches Royal three until the criteria is satisfied, (optionally) using 100% catch cheat.  </summary>
    EncounterSnack = 7_005,

    /// <summary> Keeps running circles until all party members have the Partner mark.  </summary>
    PartnerMark = 7_050,

    /// <summary> Enables testing pointers from a text file.  </summary>
    Pointer = 7_101
}

public static class PokeRoutineTypeExtensions
{
    public static bool IsTradeBot(this PokeRoutineType type) => type is >= PokeRoutineType.FlexTrade and <= PokeRoutineType.Dump;
}
