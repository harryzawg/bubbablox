using System.Text.RegularExpressions;
using Dapper;
using Roblox.Dto.Economy;
using Roblox.Dto.Users;
using Roblox.Libraries.Exceptions;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Services.Exceptions;

namespace Roblox.Services;


public class FilterService : ServiceBase, IService
{
    public bool IsReusable()
    {
        throw new NotImplementedException();
    }

    public bool IsThreadSafe()
    {
        throw new NotImplementedException();
    }
    public bool ContainsCyrillic(string input)
    {
        Regex regex = new Regex(@"[\u0400-\u04FF]");
        return regex.IsMatch(input);
    }        
    public string FilterText(string input)
    {
        string buildFilteredWordPatern(string word)
        {
            return @"\b" + string.Join(@"\s*", word.ToCharArray()) + @"\b";
        }
        string[] filteredWords = 
        {
            "I HATE jongus",
			"Ag",
			"Zachydrama",
			"Spongebob tower defense"
        };
        string[] filteredWordsPatterns = filteredWords.Select(word => buildFilteredWordPatern(word)).ToArray();
        foreach (string pattern in filteredWordsPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                return new string('#', input.Length);
            }
        }
        return input;
    }
}
