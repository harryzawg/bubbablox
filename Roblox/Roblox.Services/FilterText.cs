using System.Globalization;
using System.Text;

namespace Roblox.Services;


public class FilterService : ServiceBase, IService
{
    private static readonly string[] filteredWords =
    {
        "anal",
        "anally",
        "anus",
        "ballsac",
        "ballsack",
        "beastiality",
        "beastility",
        "bestiality",
        "blowjob",
        "blowjobs",
        "boner",
        "bitch",
        "boob",
        "boobies",
        "boobs",
        "breast",
        "breasts",
        "buttfuck",
        "buttfucker",
        "cock",
        "cockride",
        "cocks",
        "cocksuck",
        "cocksucked",
        "cocksucker",
        "cocksucking",
        "cocksucks",
        "condom",
        "condoms",
        "condo",
        "cum",
        "cummer ",
        "cumming",
        "cums",
        "cumshot",
        "cunilingus",
        "cunillingus",
        "cunnilingus",
        "dick",
        "dicks",
        "dildo",
        "dildos",
        "dildos",
        "digga",
        "ejaculate ",
        "ejaculated ",
        "ejaculates",
        "ejaculating",
        "ejaculatings",
        "ejaculation",
        "faget",
        "fagg",
        "fag",
        "fagget",
        "fagging",
        "faggit",
        "faggot",
        "faggots",
        "faggs",
        "fagit",
        "fagot",
        "fagots",
        "fingerfuck",
        "fingerfucked",
        "fingerfucker",
        "fingerfuckers",
        "fingerfucking",
        "fingerfucks",
        "fistfuck",
        "fistfucked",
        "fistfucker",
        "fistfuckers",
        "fistfucking",
        "fistfuckings",
        "fistfucks",
        "gangbang",
        "gangbanged",
        "gangbangs",
        "gangbangs",
        "gaysex",
        "hardcoresex",
        "hitler",
        "horniest",
        "horny",
        "hotsex",
        "jackingoff",
        "jackoff",
        "jackxoff",
        "jerkxoff",
        "kidsinasanbox",
        "kkk",
        "masterbait",
        "masterbate",
        "masturbate",
        "molest",
        "mycock",
        "nazi",
        "nazis",
        "niger",
        "nigger",
        "niigger",
        "niggers",
        "niiggers",
        "ngga",
        "negger",
        "neckhurt",
        "nigga",
        "n0gga",
        "nhigga",
        "n8ggas",
        "niigga",
        "niga",
        "ni$$a",
        "ni$$as",
        "nude",
        "nudism",
        "nudist",
        "orgasim",
        "orgasims",
        "orgasm",
        "orgasms",
        "pern",
        "pecker",
        "pedo",
        "pedobear",
        "penis",
        "phonesex",
        "porn",
        "pron",
        "porno",
        "pornography",
        "goon",
        "pornos",
        "pren",
        "prostitute",
        "paygorn",
        "raip",
        "raiping",
        "rape",
        "raped",
        "raper",
        "raping",
        "rapist",
        "schlong",
        "sex",
        "sexx",
        "sexxx",
        "sexxy",
        "sexytiem",
        "sexytime",
        "slut",
        "sluts ",
        "sperm",
        "strip",
        "stripper",
        "stripperpole",
        "strippers",
        "swastika",
        "thong",
        "titties",
        "titty",
        "urcock",
        "vaggina",
        "vagina",
        "vegina",
        "vibrator",
        "wanker",
        "whore",
        "whorehouse",
        "yourcock",
        "femb",
        "fembx",
        "jerkingoff",
        "jerkoff",
        "jackoff",
        "jackingoff",
        "kys",
        "killyourself",
        "killurself",
        "retard"
     };
    private static readonly HashSet<string> _filteredWordsSet = new HashSet<string>(filteredWords);
    public string FilterText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        
        string cleanedInput = string.Join("", input.ToCharArray()
            .Where(c => !char.IsWhiteSpace(c))
            .Select(char.ToLower)
            .Select(c =>
            {
            /* This will prevent words like n!igga, n!gg@ etc */
            switch (c)
            {
                case '#': return '\0';
                case '.': return '\0';
                case '$': return 's';
                case '@': return 'a';
                case '!': return 'i';
                case '0': return 'o';
                case '*': return '\0';
                case 'я': return 'r';
                default: return c;
            }
            })
            .Where(c => c != '\0')
            .ToArray());

        if (_filteredWordsSet.Any(word => cleanedInput.Contains(word)))
        {
            return new string('#', input.Length);
        }
        return input;
    }
    public string CleanText(string input)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in input.Normalize(NormalizationForm.FormC))
        {
            if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString();
    }

    public bool IsReusable()
    {
        return true;
    }

    public bool IsThreadSafe()
    {
        return true;
    }
}
