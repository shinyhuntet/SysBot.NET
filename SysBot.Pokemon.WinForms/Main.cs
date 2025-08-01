using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Z3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.Design.AxImporter;

namespace SysBot.Pokemon.WinForms;

public sealed partial class Main : Form
{
    private readonly List<PokeBotState> _bots = [];
    private readonly IPokeBotRunner _runningEnvironment;
    private readonly ProgramConfig _config;

    public Main()
    {
        InitializeComponent();

        PokeTradeBotSWSH.SeedChecker = new Z3SeedSearchHandler<PK8>();
        if (File.Exists(Program.ConfigPath))
        {
            var lines = File.ReadAllText(Program.ConfigPath);
            _config = JsonSerializer.Deserialize(lines, ProgramConfigContext.Default.ProgramConfig) ?? new ProgramConfig();

            if (string.IsNullOrWhiteSpace(_config.Hub.LoggingFolder))
            {
                // Always set logging folder to default, when it's not set
                _config.Hub.CreateDefaults(Program.WorkingDirectory);
            }

            LogConfig.MaxArchiveFiles = _config.Hub.MaxArchiveFiles;
            LogConfig.LoggingEnabled = _config.Hub.LoggingEnabled;
            LogConfig.LoggingFolder = _config.Hub.LoggingFolder;

            _runningEnvironment = GetRunner(_config);
            foreach (var bot in _config.Bots)
            {
                bot.Initialize();
                AddBot(bot);
            }
        }
        else
        {
            _config = new ProgramConfig();
            _runningEnvironment = GetRunner(_config);
            _config.Hub.Folder.CreateDefaults(Program.WorkingDirectory);
            _config.Hub.Pointer.CreateDefaults(Program.WorkingDirectory);
            _config.Hub.EncounterSV.CreateDefaults(Program.WorkingDirectory);
            _config.Hub.CreateDefaults(Program.WorkingDirectory);
        }

        var build = string.Empty;
#if DEBUG
        var date = File.GetLastWriteTime(System.Reflection.Assembly.GetEntryAssembly()!.Location);
        build = $" (dev-{date:yyyyMMdd})";
#endif
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;

        RTB_Logs.MaxLength = 32_767; // character length
        LoadControls();
        Text = $"{Text} v{v}{build} ({_config.Mode})";
        Task.Run(BotMonitor);

        InitUtil.InitializeStubs(_config.Mode);
    }

    private static IPokeBotRunner GetRunner(ProgramConfig cfg) => cfg.Mode switch
    {
        ProgramMode.SWSH => new PokeBotRunnerImpl<PK8>(cfg.Hub, new BotFactory8SWSH()),
        ProgramMode.BDSP => new PokeBotRunnerImpl<PB8>(cfg.Hub, new BotFactory8BS()),
        ProgramMode.LA => new PokeBotRunnerImpl<PA8>(cfg.Hub, new BotFactory8LA()),
        ProgramMode.SV => new PokeBotRunnerImpl<PK9>(cfg.Hub, new BotFactory9SV()),
        _ => throw new IndexOutOfRangeException("Unsupported mode."),
    };

    private async Task BotMonitor()
    {
        while (!Disposing)
        {
            try
            {
                foreach (var c in FLP_Bots.Controls.OfType<BotController>())
                    c.ReadState();
            }
            catch
            {
                // Updating the collection by adding/removing bots will change the iterator
                // Can try a for-loop or ToArray, but those still don't prevent concurrent mutations of the array.
                // Just try, and if failed, ignore. Next loop will be fine. Locks on the collection are kinda overkill, since this task is not critical.
            }
            await Task.Delay(2_000).ConfigureAwait(false);
        }
    }

    private void LoadControls()
    {
        MinimumSize = Size;
        PG_Hub.SelectedObject = _runningEnvironment.Config;

        var routines = ((PokeRoutineType[])Enum.GetValues(typeof(PokeRoutineType))).Where(z => _runningEnvironment.SupportsRoutine(z));
        var list = routines.Select(z => new ComboItem(z.ToString(), (int)z)).ToArray();
        CB_Routine.DisplayMember = nameof(ComboItem.Text);
        CB_Routine.ValueMember = nameof(ComboItem.Value);
        CB_Routine.DataSource = list;
        CB_Routine.SelectedValue = (int)PokeRoutineType.FlexTrade; // default option

        var protocols = (SwitchProtocol[])Enum.GetValues(typeof(SwitchProtocol));
        var listP = protocols.Select(z => new ComboItem(z.ToString(), (int)z)).ToArray();
        CB_Protocol.DisplayMember = nameof(ComboItem.Text);
        CB_Protocol.ValueMember = nameof(ComboItem.Value);
        CB_Protocol.DataSource = listP;
        CB_Protocol.SelectedIndex = (int)SwitchProtocol.WiFi; // default option

        LogUtil.Forwarders.Add((AppendLog, nameof(Main)));
    }

    private void AppendLog(string message, string identity)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] - {identity}: {message}{Environment.NewLine}";
        if (InvokeRequired)
            Invoke((MethodInvoker)(() => UpdateLog(line)));
        else
            UpdateLog(line);
    }

    private readonly object _logLock = new();

    private void UpdateLog(string line)
    {
        lock (_logLock)
        {
            // ghetto truncate
            var rtb = RTB_Logs;
            var text = rtb.Text;
            var max = rtb.MaxLength;
            if (text.Length + line.Length + 2 >= max)
                rtb.Text = text[(max / 4)..];

            rtb.AppendText(line);
            rtb.ScrollToCaret();
        }
    }

    private ProgramConfig GetCurrentConfiguration()
    {
        _config.Bots = _bots.ToArray();
        return _config;
    }

    private void Main_FormClosing(object sender, FormClosingEventArgs e)
    {
        SaveCurrentConfig();
        var bots = _runningEnvironment;
        if (!bots.IsRunning)
            return;

        async Task WaitUntilNotRunning()
        {
            while (bots.IsRunning)
                await Task.Delay(10).ConfigureAwait(false);
        }

        // Try to let all bots hard-stop before ending execution of the entire program.
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        bots.StopAll();
        Task.WhenAny(WaitUntilNotRunning(), Task.Delay(5_000)).ConfigureAwait(true).GetAwaiter().GetResult();
    }

    private void SaveCurrentConfig()
    {
        var cfg = GetCurrentConfiguration();
        var lines = JsonSerializer.Serialize(cfg, ProgramConfigContext.Default.ProgramConfig);
        File.WriteAllText(Program.ConfigPath, lines);
    }

    [JsonSerializable(typeof(ProgramConfig))]
    [JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    public sealed partial class ProgramConfigContext : JsonSerializerContext { }

    private void B_Start_Click(object sender, EventArgs e)
    {
        SaveCurrentConfig();

        LogUtil.LogInfo("Starting all bots...", "Form");
        _runningEnvironment.InitializeStart();
        SendAll(BotControlCommand.Start);
        Tab_Logs.Select();

        if (_bots.Count == 0)
            WinFormsUtil.Alert("No bots configured, but all supporting services have been started.");
    }

    private void SendAll(BotControlCommand cmd)
    {
        foreach (var c in FLP_Bots.Controls.OfType<BotController>())
            c.SendCommand(cmd, false);

        EchoUtil.Echo($"All bots have been issued a command to {cmd}.");
    }

    private void B_Stop_Click(object sender, EventArgs e)
    {
        var env = _runningEnvironment;
        if (!env.IsRunning && (ModifierKeys & Keys.Alt) == 0)
        {
            WinFormsUtil.Alert("Nothing is currently running.");
            return;
        }

        var cmd = BotControlCommand.Stop;

        if ((ModifierKeys & Keys.Control) != 0 || (ModifierKeys & Keys.Shift) != 0) // either, because remembering which can be hard
        {
            if (env.IsRunning)
            {
                WinFormsUtil.Alert("Commanding all bots to Idle.", "Press Stop (without a modifier key) to hard-stop and unlock control, or press Stop with the modifier key again to resume.");
                cmd = BotControlCommand.Idle;
            }
            else
            {
                WinFormsUtil.Alert("Commanding all bots to resume their original task.", "Press Stop (without a modifier key) to hard-stop and unlock control.");
                cmd = BotControlCommand.Resume;
            }
        }
        SendAll(cmd);
    }

    private void B_New_Click(object sender, EventArgs e)
    {
        var cfg = CreateNewBotConfig();
        if (!AddBot(cfg))
        {
            WinFormsUtil.Alert("Unable to add bot; ensure details are valid and not duplicate with an already existing bot.");
            return;
        }
        System.Media.SystemSounds.Asterisk.Play();
    }

    private bool AddBot(PokeBotState cfg)
    {
        if (!cfg.IsValid())
            return false;

        if (_bots.Any(z => z.Connection.Equals(cfg.Connection)))
            return false;

        PokeRoutineExecutorBase newBot;
        try
        {
            Console.WriteLine($"Current Mode ({_config.Mode}) does not support this type of bot ({cfg.CurrentRoutineType}).");
            newBot = _runningEnvironment.CreateBotFromConfig(cfg);
        }
        catch
        {
            return false;
        }

        try
        {
            _runningEnvironment.Add(newBot);
        }
        catch (ArgumentException ex)
        {
            WinFormsUtil.Error(ex.Message);
            return false;
        }

        AddBotControl(cfg);
        _bots.Add(cfg);
        return true;
    }

    private void AddBotControl(PokeBotState cfg)
    {
        var row = new BotController { Width = FLP_Bots.Width };
        row.Initialize(_runningEnvironment, cfg);
        FLP_Bots.Controls.Add(row);
        FLP_Bots.SetFlowBreak(row, true);
        row.Click += (s, e) =>
        {
            var details = cfg.Connection;
            TB_IP.Text = details.IP;
            NUD_Port.Text = details.Port.ToString();
            CB_Protocol.SelectedIndex = (int)details.Protocol;
            CB_Routine.SelectedValue = (int)cfg.InitialRoutine;
        };

        row.Remove += (s, e) =>
        {
            _bots.Remove(row.State);
            _runningEnvironment.Remove(row.State, !_runningEnvironment.Config.SkipConsoleBotCreation);
            FLP_Bots.Controls.Remove(row);
        };
    }

    private PokeBotState CreateNewBotConfig()
    {
        var ip = TB_IP.Text;
        var port = int.TryParse(NUD_Port.Text, out var p) ? p : 6000;
        var cfg = BotConfigUtil.GetConfig<SwitchConnectionConfig>(ip, port);
        cfg.Protocol = (SwitchProtocol)WinFormsUtil.GetIndex(CB_Protocol);

        var pk = new PokeBotState { Connection = cfg };
        var type = (PokeRoutineType)WinFormsUtil.GetIndex(CB_Routine);
        pk.Initialize(type);
        return pk;
    }

    private void FLP_Bots_Resize(object sender, EventArgs e)
    {
        foreach (var c in FLP_Bots.Controls.OfType<BotController>())
            c.Width = FLP_Bots.Width;
    }

    private void CB_Protocol_SelectedIndexChanged(object sender, EventArgs e)
    {
        var isWifi = CB_Protocol.SelectedIndex == 0;
        TB_IP.Visible = isWifi;
        NUD_Port.ReadOnly = isWifi;

        if (isWifi)
            NUD_Port.Text = "6000";
    }
}
