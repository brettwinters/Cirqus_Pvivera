using System;
using System.Linq;

namespace d60.Cirqus.Tests
{
    class SqlTestHelperBase
    {
        public static string GetDatabaseName(string connectionString)
        {
            var relevantSetting = connectionString
                .Split(';')
                .Select(pair => pair.Trim())
                .Select(kvp =>
                {
                    var tokens = kvp.Split('=');

                    return new
                    {
                        Key = tokens.First().Trim(),
                        Value = tokens.LastOrDefault()?.Trim()
                    };
                })
                .FirstOrDefault(a => string.Equals(a.Key, "database", StringComparison.InvariantCultureIgnoreCase));

            return relevantSetting?.Value;
        }
    }
}