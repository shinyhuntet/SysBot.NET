using PKHeX.Core;
using SysBot.Base;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSV;
using static System.Buffers.Binary.BinaryPrimitives;

namespace SysBot.Pokemon;

public abstract class PokeRoutineExecutor9SV(PokeBotState cfg) : PokeRoutineExecutor<PK9>(cfg)
{
    protected PokeDataOffsetsSV Offsets { get; } = new();

    public override async Task<PK9> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

    public override async Task<PK9> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
        return new PK9(data);
    }

    public override async Task<PK9> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return new PK9();

        return await ReadPokemon(offset, token).ConfigureAwait(false);
    }

    public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
    {
        var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
        return !result.SequenceEqual(original);
    }
    public async Task<(bool, byte, int)> ReadEmptySlot(bool ReadSlot, byte CurrentBox, int CurrentSlot, CancellationToken token)
    {
        var BoxSize = 30 * BoxFormatSlotSize;
        var wait = 0;
        ulong CurrentBoxIndexOffset = 0;
        ulong BoxStartOffset = 0;
        while (CurrentBoxIndexOffset == 0)
        {
            await Task.Delay(0_300, token).ConfigureAwait(false);
            wait++;
            CurrentBoxIndexOffset = await SwitchConnection.PointerAll(Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
            if (wait >= 15)
                break;
        }
        wait = 0;
        while (BoxStartOffset == 0)
        {
            await Task.Delay(0_300, token).ConfigureAwait(false);
            wait++;
            BoxStartOffset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
            if (wait >= 15)
                return (false, 0, 0);
        }
        if (!ReadSlot)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(CurrentBoxIndexOffset, 1, token).ConfigureAwait(false);
            CurrentBox = data[0];
            CurrentSlot = 0;
        }
        while (CurrentBox <= 31)
        {
            var boxstart = BoxStartOffset + (ulong)(CurrentBox * BoxSize) + (ulong)(CurrentSlot * BoxFormatSlotSize);
            var pokedata = await SwitchConnection.ReadBytesAbsoluteAsync(boxstart, BoxFormatSlotSize, token).ConfigureAwait(false);
            var pkm = new PK9(pokedata);
            bool isValid = PersonalTable.SV.IsPresentInGame(pkm.Species, pkm.Form);
            if (!isValid || pkm == null || !pkm.ChecksumValid || !pkm.Valid || pkm.Species <= 0 || (Species)pkm.Species > Species.MAX_COUNT)
                return (true, CurrentBox, CurrentSlot);
            CurrentSlot++;
            if (CurrentSlot == 30)
            {
                CurrentBox++;
                CurrentSlot = 0;
            }
        }
        return (false, 0, 0);
    }
    public async Task SetBoxPokemon(PK9 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();

        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        var (valid, b1s1) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        const int boxSize = 30 * BoxFormatSlotSize;
        var boxStart = b1s1 + (ulong)(box * boxSize);
        var slotStart = boxStart + (ulong)(slot * BoxFormatSlotSize);

        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, slotStart, token).ConfigureAwait(false);
    }

    public async Task<(PK9, byte[]?)> ReadRawBoxPokemon(int box, int slot, CancellationToken token)
    {
        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        var (valid, b1s1) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return (new PK9(), null);

        const int boxSize = 30 * BoxFormatSlotSize;
        var boxStart = b1s1 + (ulong)(box * boxSize);
        var slotStart = boxStart + (ulong)(slot * BoxFormatSlotSize);

        var copiedData = new byte[BoxFormatSlotSize];
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(slotStart, BoxFormatSlotSize, token).ConfigureAwait(false);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PK9(data), copiedData);
    }

    public override async Task<PK9> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        var (pk9, _) = await ReadRawBoxPokemon(box, slot, token).ConfigureAwait(false);
        return pk9;
    }

    public async Task<(PK9, byte[]?)> ReadRawPartyPokemon(int slot, CancellationToken token)
    {
        var jumps = PartyStartPokemonPointer(slot).ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return (new PK9(), null);

        var copiedData = new byte[BoxFormatSlotSize];
        var data = new byte[BoxFormatSlotSize];

        var partyData = await SwitchConnection.ReadBytesAbsoluteAsync(party, PartyFormatSlotSize, token).ConfigureAwait(false);
        partyData.CopyTo(data, 0);

        var stats = await ReadRawPartyStats(slot, token).ConfigureAwait(false);
        stats.CopyTo(data, PartyFormatSlotSize);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PK9(data), copiedData);
    }

    private async Task<byte[]> ReadRawPartyStats(int slot, CancellationToken token)
    {
        var jumps = Offsets.PartyStats.ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return Array.Empty<byte>();

        return await SwitchConnection.ReadBytesAbsoluteAsync(party + (uint)(slot * PartyStatsSize), PartyStatsSize, token).ConfigureAwait(false);
    }

    private async Task WritePartyStats(byte[] bytes, int slot, CancellationToken token)
    {
        var jumps = Offsets.PartyStats.ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        await SwitchConnection.WriteBytesAbsoluteAsync(bytes, party + (uint)(slot * PartyStatsSize), token).ConfigureAwait(false);
    }

    public async Task SetPartyPokemon(PK9 pkm, int slot, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();

        var jumps = PartyStartPokemonPointer(slot).ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedBoxData, party, token).ConfigureAwait(false);
        await WritePartyStats(pkm.EncryptedPartyData[^PartyStatsSize..], slot, token).ConfigureAwait(false);
    }

    public async Task SetBoxPokemonAbsolute(ulong offset, PK9 pkm, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();
        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedBoxData, offset, token).ConfigureAwait(false);
    }

    public async Task SetCurrentBox(byte box, CancellationToken token)
    {
        await SwitchConnection.PointerPoke([box], Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
    }

    public async Task<byte> GetCurrentBox(CancellationToken token)
    {
        var data = await SwitchConnection.PointerPeek(1, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
        return data[0];
    }

    public async Task<SAV9SV> IdentifyTrainer(CancellationToken token)
    {
        // Check if botbase is on the correct version or later.
        await VerifyBotbaseVersion(token).ConfigureAwait(false);

        // Check title so we can warn if mode is incorrect.
        string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
        if (title is not (ScarletID or VioletID))
            throw new Exception($"{title} is not a valid SV title. Is your mode correct?");

        // Verify the game version.
        var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
        if (!game_version.SequenceEqual(SVGameVersion))
            throw new Exception($"Game version is not supported. Expected version {SVGameVersion}, and current game version is {game_version}.");

        var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
        InitSaveData(sav);

        if (!IsValidTrainerData())
        {
            await CheckForRAMShiftingApps(token).ConfigureAwait(false);
            throw new Exception("Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for more information.");
        }

        if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
            throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

        return sav;
    }

    public Task<SAV9SV> GetFakeTrainerSAV(CancellationToken token)
    {
        return GetFakeTrainerSAV(Offsets.MyStatusPointer, token);
    }

    public async Task<SAV9SV> GetFakeTrainerSAV(IEnumerable<long> jumps, CancellationToken token)
    {
        var sav = new SAV9SV();
        var info = sav.MyStatus;
        var read = await SwitchConnection.PointerPeek(info.Data.Length, jumps, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
        return sav;
    }

    public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
    {
        Log("Detaching on startup.");
        await DetachController(token).ConfigureAwait(false);
        if (settings.ScreenOff)
        {
            Log("Turning off screen.");
            await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
        }
    }

    public async Task CleanExit(CancellationToken token)
    {
        await SetScreen(ScreenState.On, token).ConfigureAwait(false);
        Log("Detaching controllers on routine exit.");
        await DetachController(token).ConfigureAwait(false);
    }

    protected virtual async Task EnterLinkCode(int code, PokeTradeHubConfig config, CancellationToken token)
    {
        // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
        var keys = TradeUtil.GetPresses(code);
        foreach (var key in keys)
        {
            int delay = config.Timings.KeypressTime;
            await Click(key, delay, token).ConfigureAwait(false);
        }
        // Confirm Code outside of this method (allow synchronization)
    }

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        Log("Restarting the game!!");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);
    }

    public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Close out of the game
        await Click(B, 0_500, token).ConfigureAwait(false);
        await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
        await Click(X, 1_000, token).ConfigureAwait(false);
        await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
        Log("Closed out of the game!");
    }

    public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Open game.
        await Click(HOME, 0_700, token).ConfigureAwait(false);

        await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

        // Menus here can go in the order: Update Prompt -> Profile -> Starts Game
        //  The user can optionally turn on the setting if they know of a breaking system update incoming.
        if (timing.AvoidSystemUpdate)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false); // Reduce the chance of misclicking here.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
        }

        await Click(DUP, 0_600, token).ConfigureAwait(false);
        await Click(A, 0_600, token).ConfigureAwait(false);

        
        // Switch Logo and game load screen
        await Task.Delay(23_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

        while (!await IsOnTitleScreen(token).ConfigureAwait(false))
            await Task.Delay(2_000, token).ConfigureAwait(false);

        for (int i = 0; i < 8; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        var timer = 25_000;
        while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            timer -= 1_000;
            // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
            // Don't risk it if hub is set to avoid updates.
            if (timer <= 0 && !timing.AvoidSystemUpdate)
            {
                Log("Still not in the game, initiating rescue protocol!");
                var title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
                if (title != ScarletID && title != VioletID)
                {
                    await StartGame(config, token).ConfigureAwait(false);
                    return;
                }
                while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                {
                    var (PointerSet, ofs) = await OverworldPointerSet(token).ConfigureAwait(false);
                    if (!PointerSet || !await IsInTitle(token).ConfigureAwait(false))
                    {
                        Log("Overworld pointer is unset. Click A to Overworld!");
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    }
                    if (await IsOnOverworldTitle(token).ConfigureAwait(false))
                        break;
                    if(PointerSet)
                    {
                        while (!await IsOnOverworld(ofs, token).ConfigureAwait(false))
                        {
                            Log("Overworld pointer is set! Click B to close dialog!");
                            await Click(B, 0_600, token).ConfigureAwait(false);
                        }
                    }
                }
                break;
            }
        }

        await Task.Delay(2_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
        Log("Back in the overworld!");
    }

    public async Task SaveGame(CancellationToken token)
    {
        Log("Saving the game");
        await Click(X, 2_500, token).ConfigureAwait(false);
        await Click(R, 2_300, token).ConfigureAwait(false);
        await Click(A, 7_000, token).ConfigureAwait(false);

        for (var i = 0; i < 4; i++)
            await Click(B, 0_400, token).ConfigureAwait(false);
    }
    public async Task<(bool, ulong)> OverworldPointerSet(CancellationToken token)
    {
        var offset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        if (offset == 0)
            return (false, 0);
        return (true, offset);
    }
    public async Task<bool> IsOnTitleScreen(CancellationToken token)
    {
        var offset = await SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
        if (offset == 0)
            return false;
        if (!await IsInTitle(token).ConfigureAwait(false))
            return false;
        return true;
    }
    public async Task<bool> IsInTitle(CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesMainAsync(Offsets.PicnicMenu, 1, token).ConfigureAwait(false);
        return data[0] == 0x01; // 1 when in picnic, 2 in sandwich menu, 3 when eating, 2 when done eating
    }
    public async Task<bool> IsInBattle(CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesMainAsync(IsBattle, 1, token).ConfigureAwait(false);
        return data[0] <= 0x05;
    }
    public async Task<bool> IsConnectedOnline(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public async Task<ulong> GetTradePartnerNID(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 8, token).ConfigureAwait(false);
        return BitConverter.ToUInt64(data, 0);
    }

    public async Task ClearTradePartnerNID(ulong offset, CancellationToken token)
    {
        var data = new byte[8];
        await SwitchConnection.WriteBytesAbsoluteAsync(data, offset, token).ConfigureAwait(false);
    }

    public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 0x11;
    }
    public async Task<bool> IsInBattleToStatic(CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        if (!valid)
            return false;
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 0x06;
    }

    // Only used to check if we made it off the title screen; the pointer isn't viable until a few seconds after clicking A.
    private async Task<bool> IsOnOverworldTitle(CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        if (!valid)
            return false;
        return await IsOnOverworld(offset, token).ConfigureAwait(false);
    }

    // Usually 0x9-0xA if fully loaded into Poké Portal.
    public async Task<bool> IsInPokePortal(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] >= 9;
    }

    // Usually 4-6 in a box.
    public async Task<bool> IsInBox(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] < 8;
    }

    public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
    {
        var data = await SwitchConnection.PointerPeek(1, Offsets.ConfigPointer, token).ConfigureAwait(false);
        return (TextSpeedOption)(data[0] & 3);
    }

    // Switches to box 1, then clears slot1 to prep fossil and egg bots.
    public async Task SetupBoxState(IDumper dumpSetting, CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);

        var (existing, bytes) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);
        if (existing.Species != 0 && existing.ChecksumValid)
        {
            Log("Destination slot is occupied! Dumping the Pokémon found there...");
            DumpPokemon(dumpSetting.DumpFolder, "saved", existing, bytes!);
        }

        Log("Clearing destination slot to start the bot.");
        PK9 blank = new();
        await SetBoxPokemon(blank, 0, 0, token).ConfigureAwait(false);
    }

    private ulong _saveKeyAddress;

    public async Task<byte[]> ReadEncryptedBlock(ulong baseBlock, uint blockKey, bool init, CancellationToken token)
    {
        if (init)
        {
            var address = await SearchSaveKey(baseBlock, blockKey, token).ConfigureAwait(false);
            address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            _saveKeyAddress = address;
            Log($"Initial address found at {_saveKeyAddress:X8}");
        }

        var header = await SwitchConnection.ReadBytesAbsoluteAsync(_saveKeyAddress, 5, token).ConfigureAwait(false);
        header = DecryptBlock(blockKey, header);

        var size = ReadUInt32LittleEndian(header.AsSpan()[1..]);
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(_saveKeyAddress, 5 + (int)size, token).ConfigureAwait(false);
        var res = DecryptBlock(blockKey, data)[5..];

        return res;
    }

    public async Task<ulong> SearchSaveKey(ulong baseBlock, uint key, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(baseBlock + 8, 16, token).ConfigureAwait(false);
        var start = BitConverter.ToUInt64(data.AsSpan()[..8]);
        var end = BitConverter.ToUInt64(data.AsSpan()[8..]);

        while (start < end)
        {
            var block_ct = (end - start) / 48;
            var mid = start + (block_ct >> 1) * 48;

            data = await SwitchConnection.ReadBytesAbsoluteAsync(mid, 4, token).ConfigureAwait(false);
            var found = BitConverter.ToUInt32(data);
            if (found == key)
                return mid;

            if (found >= key)
                end = mid;
            else start = mid + 48;
        }
        return start;
    }

    private static byte[] DecryptBlock(uint key, byte[] block)
    {
        var rng = new SCXorShift32(key);
        for (var i = 0; i < block.Length; i++)
            block[i] = (byte)(block[i] ^ rng.Next());

        return block;
    }
}
