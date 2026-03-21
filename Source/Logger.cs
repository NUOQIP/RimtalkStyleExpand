using Verse;

namespace RimTalkStyleExpand
{
    public static class Logger
    {
        private const string Prefix = "[StyleExpand] ";

        public static void Message(string message)
        {
            Log.Message(Prefix + message);
        }

        public static void Warning(string message)
        {
            Log.Warning(Prefix + message);
        }

        public static void Error(string message)
        {
            Log.Error(Prefix + message);
        }
    }
}