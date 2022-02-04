using Newtonsoft.Json;

namespace BetterSongSearch.Util
{
    internal static class JsonHelpers
    {
        public static readonly JsonSerializerSettings leanDeserializeSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Error = (se, ev) =>
            {
#if DEBUG
                Plugin.Log.Warn("Failed JSON deserialize:");
                Plugin.Log.Warn(ev.ErrorContext.Error);
#endif
                ev.ErrorContext.Handled = true;
            }
        };
    }
}
