using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;
public class StopFilter
{
    private const string StopFilters = nameof(StopFilters);
    public override string ToString() => Name.ToString();

    public Species Species;
    public int? Form;
    public TargetGenderType Gender;
    public string[]? formstringlist;
    [Category(StopFilters), Description("Filter Name")]
    public string Name { get; set; } = "";
    [Category(StopFilters), Description("Target Rate")]
    public string Ratestring { get; set; } = string.Empty;
    [Category(StopFilters), Description("Whether enable this Filter or not")]
    public bool Enabled { get; set; }
    [Category(StopFilters), Description("Stops only on Pokémon of this species. No restrictions if set to \"None\".")]
    public Species StopOnSpecies { get { return Species; } set { Species = value; SetForm(); GenderCheck(); } }
    public string? FormString { get; set; }
    public string[]? FormList { get { return formstringlist; } set { } }

    [Category(StopFilters), Description("Stops only on Pokémon with this FormID. No restrictions if left blank.")]
    public int? StopOnForm { get { return Form; } set { Form = value; SetForm(); GenderCheck(); } }

    [Category(StopFilters), Description("Stop only on Pokémon of the specified gender.")]
    public TargetGenderType TargetGender { get { if (Species <= Species.None) { Gender = TargetGenderType.Any; } return Gender; } set { Gender = value; GenderCheck(); } }

    [Category(StopFilters), Description("Stop only on Pokémon of the specified nature.")]
    public Nature TargetNature { get; set; } = Nature.Random;

    [Category(StopFilters), Description("Minimum accepted IVs in the format HP/Atk/Def/SpA/SpD/Spe. Use \"x\" for unchecked IVs and \"/\" as a separator.")]
    public string TargetMinIVs { get; set; } = "";

    [Category(StopFilters), Description("Maximum accepted IVs in the format HP/Atk/Def/SpA/SpD/Spe. Use \"x\" for unchecked IVs and \"/\" as a separator.")]
    public string TargetMaxIVs { get; set; } = "";

    [Category(StopFilters), Description("Selects the shiny type to stop on.")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopFilters), Description("Stop only on Pokémon that have a mark.")]
    public bool MarkOnly { get; set; }

    [Category(StopFilters), Description("List of marks to ignore separated by commas. Use the full name, e.g. \"Uncommon Mark, Dawn Mark, Prideful Mark\".")]
    public string UnwantedMarks { get; set; } = "";

    [Category(StopFilters), Description("When enabled, the bot will only stop when encounter has a Scale of XXXS or XXXL.")]
    public bool MinMaxScaleOnly { get; set; } = false;

    [Category(StopFilters), Description("When enabled, the bot will look for 3 Segment Dunsparce or Family of Three Maus.")]
    public bool OneInOneHundredOnly { get; set; } = true;

    [Category(StopFilters), Description("Holds Capture button to record a 30 second clip when a matching Pokémon is found by EncounterBot or Fossilbot.")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopFilters), Description("Extra time in milliseconds to wait after an encounter is matched before pressing Capture for EncounterBot or Fossilbot.")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopFilters), Description("If set to TRUE, matches both ShinyTarget and TargetIVs settings. Otherwise, looks for either ShinyTarget or TargetIVs match.")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopFilters), Description("If not empty, the provided string will be prepended to the result found log message to Echo alerts for whomever you specify. For Discord, use <@userIDnumber> to mention.")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;
    public virtual bool IsUnwantedMark(string mark, IReadOnlyList<string> marklist) => marklist.Contains(mark);
    private void SetForm()
    {
        GameStrings gameStrings = GameInfo.GetStrings("en");
        var TypesList = gameStrings.types;
        string[] GenderList = [.. GameInfo.GenderSymbolUnicode];
        var FormList = gameStrings.forms;
        var form = PersonalTable.SV.GetFormEntry((ushort)Species, Form == null ? (byte)0 : (byte)Form).FormCount;
        if (Form == null)
        {
            FormString = null;
            formstringlist = null;
            return;
        }
        else if (Form > form - 1 || Form < 0)
        {
            Form = 0;
        }
        var formlist = FormConverter.GetFormList((ushort)Species, TypesList, FormList, GenderList, EntityContext.Gen9);
        if (Species == Species.Minior)
            formlist = formlist.Take((formlist.Length + 1) / 2).ToArray();

        if (formlist.Length == 0 || (formlist.Length == 1 && formlist[0].Equals("")))
        {
            Form = null;
            FormString = null;
            formstringlist = null;
        }
        else
        {
            formstringlist = formlist;
            FormString = formlist[Form != null ? (int)Form : 0];
        }
    }
    private void GenderCheck()
    {
        var gender = PersonalTable.SV.GetFormEntry((ushort)Species, Form == null ? (byte)0 : (byte)Form).Gender;
        if (gender is PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicMale or PersonalInfo.RatioMagicFemale)
            Gender = TargetGenderType.Any;
        else if (Gender == TargetGenderType.Genderless)
            Gender = TargetGenderType.Any;
    }

}
public class SearchConditionSettings
{
    private const string SearchConditions = nameof(SearchConditions);
    public override string ToString() => "Stop Condition Settings";

    [Category(SearchConditions), Description("Saerch Filters")]
    public List<StopFilter> Filter { get; set; } = [];
}

public class StopConditionSettings
{
    private const string StopConditions = nameof(StopConditions);
    public override string ToString() => "Stop Condition Settings";

    [Category(StopConditions), Description("Stops only on Pokémon of this species. No restrictions if set to \"None\".")]
    public Species StopOnSpecies { get; set; }

    [Category(StopConditions), Description("Stops only on Pokémon with this FormID. No restrictions if left blank.")]
    public int? StopOnForm { get; set; }

    [Category(StopConditions), Description("Desired spreads, search for nature and IVs. In the format HP/Atk/Def/SpA/SpD/Spe. Use \"x\" for unchecked IVs and \"/\" as a separator.")]
    public List<SearchCondition> SearchConditions { get; set; } = new();

    [Category(StopConditions), Description("Selects the shiny type to stop on.")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopConditions), Description("Stop only on Pokémon that have a mark.")]
    public bool MarkOnly { get; set; }

    [Category(StopConditions), Description("List of marks to ignore separated by commas. Use the full name, e.g. \"Uncommon Mark, Dawn Mark, Prideful Mark\".")]
    public string UnwantedMarks { get; set; } = "";

    [Category(StopConditions), Description("Holds Capture button to record a 30 second clip when a matching Pokémon is found by EncounterBot or Fossilbot.")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), Description("Extra time in milliseconds to wait after an encounter is matched before pressing Capture for EncounterBot or Fossilbot.")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), Description("If set to TRUE, matches both ShinyTarget and TargetIVs settings. Otherwise, looks for either ShinyTarget or TargetIVs match.")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopConditions), Description("If not empty, the provided string will be prepended to the result found log message to Echo alerts for whomever you specify. For Discord, use <@userIDnumber> to mention.")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    [Category(StopConditions)]
    public class SearchCondition
    {
        public override string ToString() => $"{(!IsEnabled ? $"{Nature}, condition is disabled" : $"{Nature}, {TargetMinIVs} - {TargetMaxIVs}")}";

        [Category(StopConditions), DisplayName("1. Enabled")]
        public bool IsEnabled { get; set; } = true;

        [Category(StopConditions), DisplayName("2. Nature")]
        public Nature Nature { get; set; }

        [Category(StopConditions), DisplayName("3. Gender")]
        public TargetGenderType GenderTarget { get; set; } = TargetGenderType.Any;

        [Category(StopConditions), DisplayName("4. Minimum accepted IVs")]
        public string TargetMinIVs { get; set; } = "";

        [Category(StopConditions), DisplayName("5. Maximum accepted IVs")]
        public string TargetMaxIVs { get; set; } = "";
    }

    public static bool EncounterFound<T>(T pk, StopConditionSettings settings, IReadOnlyList<string>? markList) where T : PKM
    {
        // Match Nature and Species if they were specified.
        if (settings.StopOnSpecies != Species.None && settings.StopOnSpecies != (Species)pk.Species)
            return false;

        if (settings.StopOnForm.HasValue && settings.StopOnForm != pk.Form)
            return false;

        // Return if it doesn't have a mark or it has an unwanted mark.
        var unmarked = pk is IRibbonIndex m && !HasMark(m);
        var unwanted = markList is not null && pk is IRibbonIndex m2 && settings.IsUnwantedMark(GetMarkName(m2), markList);
        if (settings.MarkOnly && (unmarked || unwanted))
            return false;

        if (settings.ShinyTarget != TargetShinyType.DisableOption)
        {
            bool shinyMatch = settings.ShinyTarget switch
            {
                TargetShinyType.AnyShiny => pk.IsShiny,
                TargetShinyType.NonShiny => !pk.IsShiny,
                TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
                TargetShinyType.SquareOnly => pk.ShinyXor == 0,
                TargetShinyType.DisableOption => true,
                _ => throw new ArgumentException(nameof(TargetShinyType)),
            };

            // If we only needed to match one of the criteria and it shinymatch'd, return true.
            // If we needed to match both criteria and it didn't shinymatch, return false.
            if (!settings.MatchShinyAndIV && shinyMatch)
                return true;

            if (settings.MatchShinyAndIV && !shinyMatch)
                return false;
        }

        // Reorder the speed to be last.
        Span<int> pkIVList = stackalloc int[6];
        pk.GetIVs(pkIVList);
        (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);
        var pkIVsArr = pkIVList.ToArray();

        // No search conditions to match
        if (!settings.SearchConditions.Any(s => s.IsEnabled))
            return true;

        return settings.SearchConditions.Any(s =>
            MatchIVs(pkIVsArr, s.TargetMinIVs, s.TargetMaxIVs) &&
            (s.Nature == pk.Nature || s.Nature == Nature.Random) &&
            MatchGender(s.GenderTarget, (Gender)pk.Gender) &&
            s.IsEnabled);
    }
    public static double CalcRate(List<StopFilter> filters)
    {
        double Rate = 0.00;
        foreach(var filter in filters)
        {
            if (!filter.Enabled)
                continue;
            string rate = filter.Ratestring.Replace(" ", "");
            string[] ratearray = rate.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (ratearray.Length == 2)
            {
                if (int.TryParse(ratearray[0], out var Rate1) && int.TryParse(ratearray[1], out var Rate2))
                    Rate += (Rate1 * 1.00) / Rate2;
            }
        }
        return Rate;
    }

    public static (bool, List<StopFilter>?) EncounterListFound<T>(T pk, List<StopFilter> SettingList) where T : PKM
    {
        List<StopFilter> SatisfiedList = [];
        Span<int> pkIVList = stackalloc int[6];
        for (int index = 0; index < SettingList.Count; index++)
        {
            var settings = SettingList[index];
            if (!settings.Enabled)
                continue;

            // Match Nature and Species if they were specified.
            if (settings.StopOnSpecies != Species.None && settings.StopOnSpecies != (Species)pk.Species)
                continue;

            if (settings.StopOnForm.HasValue && settings.StopOnForm != pk.Form)
                continue;

            if (settings.TargetGender != TargetGenderType.Any && (int)settings.TargetGender != pk.Gender)
                continue;

            if (settings.TargetNature != Nature.Random && settings.TargetNature != (Nature)pk.Nature)
                continue;

            // Return if it doesn't have a mark or it has an unwanted mark.
            var unmarked = pk is IRibbonIndex m && !HasMark(m);
            ReadUnwantedMarksList(settings, out IReadOnlyList<string> marks);
            var unwanted = marks is not null && marks.Count > 0 && pk is IRibbonIndex m2 && settings.IsUnwantedMark(GetMarkName(m2), marks);
            if (settings.MarkOnly && (unmarked || unwanted))
                continue;

            if (settings.ShinyTarget != TargetShinyType.DisableOption)
            {
                bool shinymatch = settings.ShinyTarget switch
                {
                    TargetShinyType.AnyShiny => pk.IsShiny,
                    TargetShinyType.NonShiny => !pk.IsShiny,
                    TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
                    TargetShinyType.SquareOnly => pk.ShinyXor == 0,
                    TargetShinyType.DisableOption => true,
                    _ => throw new ArgumentException(nameof(TargetShinyType)),
                };

                // If we only needed to match one of the criteria and it shinymatch'd, return true.
                // If we needed to match both criteria and it didn't shinymatch, return false.
                if (!settings.MatchShinyAndIV && shinymatch)
                    SatisfiedList.Add(settings);

                if (settings.MatchShinyAndIV && !shinymatch)
                    continue;
            }

            // Reorder the speed to be last.
            pk.GetIVs(pkIVList);
            (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);
            if (!MatchIVs(pkIVList.ToArray(), settings.TargetMinIVs, settings.TargetMaxIVs))
                continue;

            SatisfiedList.Add(settings);
        }
        if (SatisfiedList is not null && SatisfiedList.Count > 0)
            return (true, SatisfiedList);
        return (false, null);
    }
    private static bool MatchGender(TargetGenderType target, Gender result)
    {
        return target switch
        {
            TargetGenderType.Any => true,
            TargetGenderType.Male => Gender.Male == result,
            TargetGenderType.Female => Gender.Female == result,
            TargetGenderType.Genderless => Gender.Genderless == result,
            _ => throw new ArgumentOutOfRangeException(nameof(target), $"{nameof(TargetGenderType)} value {target} is not valid"),
        };
    }

    private static bool MatchIVs(IReadOnlyList<int> pkIVs, string targetMinIVsStr, string targetMaxIVsStr)
    {
        var targetMinIVs = ReadTargetIVs(targetMinIVsStr, true);
        var targetMaxIVs = ReadTargetIVs(targetMaxIVsStr, false);

        for (var i = 0; i < 6; i++)
        {
            if (targetMinIVs[i] > pkIVs[i] || targetMaxIVs[i] < pkIVs[i])
                return false;
        }

        return true;
    }

    private static int[] ReadTargetIVs(string splitIVsStr, bool min)
    {
        var targetIVs = new int[6];
        char[] split = ['/'];

        var splitIVs = splitIVsStr.Split(split, StringSplitOptions.RemoveEmptyEntries);

        // Only accept up to 6 values. Fill it in with default values if they don't provide 6.
        // Anything that isn't an integer will be a wild card.
        for (var i = 0; i < 6; i++)
        {
            if (i < splitIVs.Length)
            {
                var str = splitIVs[i];
                if (int.TryParse(str, out var val))
                {
                    targetIVs[i] = val;
                    continue;
                }
            }
            targetIVs[i] = min ? 0 : 31;
        }
        return targetIVs;
    }

    public static bool HasMark(IRibbonIndex pk)
    {
        return HasMark(pk, out _);
    }

    public static bool HasMark(IRibbonIndex pk, out RibbonIndex result)
    {
        result = default;
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
            {
                result = mark;
                return true;
            }
        }
        return false;
    }

    public string GetPrintName(PKM pk)
    {
        var set = ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetMarkName(r);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon found to have **{GetMarkName(r)}**!";
        }
        return set;
    }

    public static void ReadUnwantedMarks(StopConditionSettings settings, out IReadOnlyList<string> marks) =>
        marks = settings.UnwantedMarks.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
    public static void ReadUnwantedMarksList(StopFilter settings, out IReadOnlyList<string> marks) =>
        marks = settings.UnwantedMarks.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    public virtual bool IsUnwantedMark(string mark, IReadOnlyList<string> marklist) => marklist.Contains(mark);

    public static string GetMarkName(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return RibbonStrings.GetName($"Ribbon{mark}");
        }
        return "";
    }
}

public enum TargetShinyType
{
    DisableOption,  // Doesn't care
    NonShiny,       // Match nonshiny only
    AnyShiny,       // Match any shiny regardless of type
    StarOnly,       // Match star shiny only
    SquareOnly,     // Match square shiny only
}

public enum TargetGenderType
{
    Any,            // Doesn't care
    Male,           // Match male only
    Female,         // Match female only
    Genderless,     // Match genderless only
}
