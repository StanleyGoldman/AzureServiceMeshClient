using System;

namespace Client.App
{
    internal static class Common
    {
        public static string CleanGuid()
        {
            return Guid.NewGuid().ToString().Replace("-", String.Empty);
        }
    }
}