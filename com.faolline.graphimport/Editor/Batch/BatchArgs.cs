using System.Collections.Generic;

namespace Faolline.GraphImport.Editor
{
    /// <summary>Shared `-flag value` command-line parsing for the batch/-executeMethod entry points.</summary>
    public static class BatchArgs
    {
        public static Dictionary<string, string> Parse(string[] rawArgs)
        {
            var map = new Dictionary<string, string>();
            for (var i = 0; i < rawArgs.Length - 1; i++)
                if (rawArgs[i].StartsWith("-"))
                    map[rawArgs[i]] = rawArgs[i + 1];
            return map;
        }
    }
}
