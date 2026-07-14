using System;
using System.Collections.Generic;

namespace CierzoArena.Online.Room
{
    /// <summary>
    /// Compact player-owned chat history. Each player publishes their latest messages
    /// through their replicated session properties, so the room does not require a
    /// separate chat server before the match transport has started.
    /// </summary>
    public readonly struct RoomChatEntry
    {
        public readonly long Timestamp;
        public readonly string Text;

        public RoomChatEntry(long timestamp, string text) { Timestamp = timestamp; Text = text ?? string.Empty; }
    }

    public static class RoomChatHistory
    {
        private const char EntrySeparator = ';';
        private const char ValueSeparator = '|';
        public const int MaxMessagesPerPlayer = 8;
        public const int MaxMessageLength = 180;

        public static string Append(string history, string message, long timestamp)
        {
            List<RoomChatEntry> entries = Parse(history);
            entries.Add(new RoomChatEntry(timestamp, NormalizeMessage(message)));
            if (entries.Count > MaxMessagesPerPlayer) entries.RemoveRange(0, entries.Count - MaxMessagesPerPlayer);
            return Serialize(entries);
        }

        public static List<RoomChatEntry> Parse(string history)
        {
            List<RoomChatEntry> entries = new List<RoomChatEntry>();
            if (string.IsNullOrWhiteSpace(history)) return entries;
            foreach (string item in history.Split(EntrySeparator))
            {
                string[] values = item.Split(ValueSeparator);
                if (values.Length != 2 || !long.TryParse(values[0], out long timestamp)) continue;
                try
                {
                    string text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(values[1]));
                    if (!string.IsNullOrWhiteSpace(text)) entries.Add(new RoomChatEntry(timestamp, NormalizeMessage(text)));
                }
                catch (FormatException) { }
            }
            return entries;
        }

        public static string NormalizeMessage(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > MaxMessageLength) normalized = normalized.Substring(0, MaxMessageLength);
            return normalized.Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Serialize(List<RoomChatEntry> entries)
        {
            List<string> values = new List<string>(entries.Count);
            foreach (RoomChatEntry entry in entries)
                values.Add(entry.Timestamp + ValueSeparator.ToString() + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(entry.Text)));
            return string.Join(EntrySeparator.ToString(), values);
        }
    }
}
