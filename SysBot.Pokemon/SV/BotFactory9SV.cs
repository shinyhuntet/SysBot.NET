using PKHeX.Core;
using System;

namespace SysBot.Pokemon;

public sealed class BotFactory9SV : BotFactory<PK9>
{
    public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK9> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
    {
        PokeRoutineType.EggFetch => new EncounterBotEggSV(cfg, Hub),
        PokeRoutineType.Reset => new EncounterBotResetSV(cfg, Hub),
        PokeRoutineType.EncounterRuinous => new EncounterBotRuinousSV(cfg, Hub),
        PokeRoutineType.EncounterGimmighoul => new EncounterBotGimmighoulSV(cfg, Hub),
        PokeRoutineType.EncounterLoyal => new EncounterBotLoyalSV(cfg, Hub),
        PokeRoutineType.EncounterParadox => new EncounterBotParadoxSV(cfg, Hub),
        PokeRoutineType.EncounterSnack => new EncounterBotSnackSV(cfg, Hub),
        PokeRoutineType.EncounterOverworld => new EncounterBotOverworldScanner(cfg, Hub),
        PokeRoutineType.RemoteControl => new RemoteControlBotSV(cfg),
        PokeRoutineType.Pointer => new PointerBotSV(cfg, Hub),
        PokeRoutineType.PartnerMark => new PartnerMarkBot(cfg, Hub),

        _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
    };

    public override bool SupportsRoutine(PokeRoutineType type) => type switch
    {
        PokeRoutineType.EggFetch => true,
        PokeRoutineType.Reset => true,
        PokeRoutineType.EncounterRuinous => true,
        PokeRoutineType.EncounterGimmighoul => true,
        PokeRoutineType.EncounterLoyal => true,
        PokeRoutineType.EncounterParadox => true,
        PokeRoutineType.EncounterSnack => true,
        PokeRoutineType.EncounterOverworld => true,
        PokeRoutineType.RemoteControl => true,
        PokeRoutineType.Pointer => true,
        PokeRoutineType.PartnerMark => true,

        _ => false,
    };
}
