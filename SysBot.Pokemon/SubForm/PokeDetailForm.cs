using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms;

public partial class PokeDetailForm : Form
{
    private Image? ShinySquare = null!;
    private Image? ShinyStar = null!;
    public PokeDetailForm()
    {
        InitializeComponent();
        Task.Run(async () => await AnticipateResponse(CancellationToken.None).ConfigureAwait(false));
        SyncContextHolder.SyncContext = SynchronizationContext.Current;
    }
    public void RefreshComponents()
    {
        InitializeComponent();
    }
    private async Task AnticipateResponse(CancellationToken token)
    {
        using HttpClient client = new();
        string shinyicon = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.WinForms/Resources/img/Markings/";
        var square = await client.GetStreamAsync(shinyicon + "rare_icon_2.png", token).ConfigureAwait(false);
        ShinySquare = Image.FromStream(square);

        var star = await client.GetStreamAsync(shinyicon + "rare_icon.png", token).ConfigureAwait(false);
        ShinyStar = Image.FromStream(star);
    }
    public void ResetAssets()
    {
        PokemonText.Text = string.Empty;
        PokePic.Image = null;
        ShinyPic.Image = null;
        TypePic.Image = null;
        MarkPic.Image = null;
    }
    public async Task SetPokeDetail(PKM pk, int EncounterCount, double Rate, CancellationToken token)
    {
        await SetPokeImage(pk, token).ConfigureAwait(false);
        SetShinyImage(pk);
        if(pk is PK9 pk9)
            SetGemImage((int)pk9.TeraType);
        SetMarkImage(pk);
        SetPrintName(pk, EncounterCount, Rate);
    }
    private void SetPrintName(PKM pk, int EncounterCount, double Rate)
    {
        var set = $"Encounter: {EncounterCount}{Environment.NewLine}Target Rate: {Rate:0.0000}%{Environment.NewLine}";
        set += ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetRibbonName(r, out _);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon found to have **{rstring}**!";
        }
        PokemonText.Text = set;
    }
    private string GetRibbonName(IRibbonIndex pk, out RibbonIndex ribbon)
    {
        ribbon = RibbonIndex.MAX_COUNT;
        for (var mark = RibbonIndex.ChampionKalos; mark <= RibbonIndex.MarkTitan; mark++)
        {
            if (pk.GetRibbon((int)mark))
            {
                ribbon = mark;
                return RibbonStrings.GetName($"Ribbon{mark}");
            }
        }
        return "";
    }
    private async Task SetPokeImage(PKM pk, CancellationToken token)
    {
        using HttpClient client = new();
        var sprite = PokeImg(pk, false);
        var response = await client.GetStreamAsync(sprite, token).ConfigureAwait(false);
        Image img = Image.FromStream(response);
        var img2 = (Image)new Bitmap(img, new Size(img.Width, img.Height));
        if(pk is PK9 pk9)
            img2 = ApplyTeraColor((byte)pk9.TeraType, img2, SpriteBackgroundType.BottomStripe);
        PokePic.Image = img2;
    }
    private void SetMarkImage(PKM pk)
    {
        if (pk is IRibbonIndex r)
        {
            var ribbonstring = GetRibbonName(r, out RibbonIndex mark);
            if (string.IsNullOrEmpty(ribbonstring))
                return;
            string url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/ribbonmark{mark.ToString().Replace("Mark", "").ToLower()}.png";
            MarkPic.Load(url);
        }
    }
    private void SetShinyImage(PKM pk)
    {
        if (pk.IsShiny)
        {
            Image? shiny = pk.ShinyXor == 0 ? ShinySquare : ShinyStar;
            ShinyPic.Image = shiny;
        }
    }
    private void SetGemImage(int teratype)
    {
        var baseurl = $"https://raw.githubusercontent.com/LegoFigure11/RaidCrawler/main/RaidCrawler.WinForms/Resources/gem_{teratype:D2}.png";
        PictureBox picture = new();
        picture.Load(baseurl);
        var baseImg = picture.Image;
        if (baseImg is null)
            return;

        var backlayer = new Bitmap(baseImg.Width + 10, baseImg.Height + 10, baseImg.PixelFormat);
        baseImg = ImageUtil.LayerImage(backlayer, baseImg, 5, 5);
        var pixels = ImageUtil.GetPixelData((Bitmap)baseImg);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
            }
        }

        baseImg = ImageUtil.GetBitmap(pixels, baseImg.Width, baseImg.Height, baseImg.PixelFormat);
        TypePic.Image = baseImg;
    }
    private Image ApplyTeraColor(byte elementalType, Image img, SpriteBackgroundType type)
    {
        var color = TypeColor.GetTypeSpriteColor(elementalType);
        var thk = SpriteBuilder.ShowTeraThicknessStripe;
        var op = SpriteBuilder.ShowTeraOpacityStripe;
        var bg = SpriteBuilder.ShowTeraOpacityBackground;
        return ApplyColor(img, type, color, thk, op, bg);
    }
    private Image ApplyColor(Image img, SpriteBackgroundType type, Color color, int thick, byte opacStripe, byte opacBack)
    {
        if (type == SpriteBackgroundType.BottomStripe)
        {
            int stripeHeight = thick; // from bottom
            if ((uint)stripeHeight > img.Height) // clamp negative & too-high values back to height.
                stripeHeight = img.Height;

            return ImageUtil.BlendTransparentTo(img, color, opacStripe, img.Width * 4 * (img.Height - stripeHeight));
        }
        if (type == SpriteBackgroundType.TopStripe)
        {
            int stripeHeight = thick; // from top
            if ((uint)stripeHeight > img.Height) // clamp negative & too-high values back to height.
                stripeHeight = img.Height;

            return ImageUtil.BlendTransparentTo(img, color, opacStripe, 0, (img.Width * 4 * stripeHeight) - 4);
        }
        if (type == SpriteBackgroundType.FullBackground) // full background
            return ImageUtil.BlendTransparentTo(img, color, opacBack);
        return img;
    }
    private string PokeImg(PKM pkm, bool canGmax)
    {
        bool md = false;
        bool fd = false;
        string[] baseLink;
        baseLink = "https://raw.githubusercontent.com/zyro670/HomeImages/master/512x512/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');

        if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form is 0)
        {
            if (pkm.Gender is 0 && pkm.Species is not (ushort)Species.Torchic)
                md = true;
            else fd = true;
        }

        int form = pkm.Species switch
        {
            (ushort)Species.Sinistea or (ushort)Species.Polteageist or (ushort)Species.Rockruff or (ushort)Species.Mothim => 0,
            (ushort)Species.Alcremie when pkm.IsShiny || canGmax => 0,
            _ => pkm.Form,

        };

        if (pkm.Species is (ushort)Species.Sneasel)
        {
            if (pkm.Gender is 0)
                md = true;
            else fd = true;
        }

        if (pkm.Species is (ushort)Species.Basculegion)
        {
            if (pkm.Gender is 0)
            {
                md = true;
                pkm.Form = 0;
            }
            else
            {
                pkm.Form = 1;
            }

            string s = pkm.IsShiny ? "r" : "n";
            string g = md && pkm.Gender is not 1 ? "md" : "fd";
            return $"https://raw.githubusercontent.com/zyro670/HomeImages/master/128x128/poke_capture_0" + $"{pkm.Species}" + "_00" + $"{pkm.Form}" + "_" + $"{g}" + "_n_00000000_f_" + $"{s}" + ".png";
        }

        baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : pkm.Species >= 1000 ? $"{pkm.Species}" : $"0{pkm.Species}";
        baseLink[3] = pkm.Form < 10 ? $"00{form}" : $"0{form}";
        baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
        baseLink[5] = canGmax ? "g" : "n";
        baseLink[6] = "0000000" + (pkm.Species is (ushort)Species.Alcremie && !canGmax ? pkm.Data[0xD0] : 0);
        baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
        return string.Join("_", baseLink);
    }

}
public static class SyncContextHolder
{
    public static SynchronizationContext? SyncContext { get; set; }
}

