using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Straumr.Core.Helpers;

public static class JsoncCommentHelpers
{
    private const string Root = "$";
    private const char MemberMark = '\u0001';
    private const char IndexMark = '\u0002';

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Allow,
        AllowTrailingCommas = true,
        MaxDepth = 256
    };

    public static string Carry(string? previous, string rewritten)
    {
        if (string.IsNullOrWhiteSpace(previous))
        {
            return rewritten;
        }

        IReadOnlyList<JsoncCommentModel> comments = Capture(previous);
        return comments.Count == 0 ? rewritten : Apply(rewritten, comments);
    }

    public static IReadOnlyList<JsoncCommentModel> Capture(string jsonc)
    {
        if (string.IsNullOrWhiteSpace(jsonc))
        {
            return [];
        }

        byte[] bytes = Encoding.UTF8.GetBytes(jsonc);
        List<JsoncCommentModel> comments = [];
        List<string> pending = [];
        Stack<JsoncFrameModel> stack = new();
        string? pendingName = null;
        string? lastCompleted = null;
        bool rootDone = false;
        int previousEnd = 0;

        try
        {
            Utf8JsonReader reader = new(bytes, ReaderOptions);
            while (reader.Read())
            {
                int start = (int)reader.TokenStartIndex;
                int end = (int)reader.BytesConsumed;

                if (reader.TokenType == JsonTokenType.Comment)
                {
                    string text = Encoding.UTF8.GetString(bytes, start, end - start).TrimEnd();
                    if (lastCompleted is not null && !HasNewline(bytes, previousEnd, start))
                    {
                        comments.Add(new JsoncCommentModel(lastCompleted, JsoncCommentPlacement.Trailing, text));
                    }
                    else if (rootDone)
                    {
                        comments.Add(new JsoncCommentModel(Root, JsoncCommentPlacement.After, text));
                    }
                    else
                    {
                        pending.Add(text);
                    }

                    previousEnd = end;
                    continue;
                }

                previousEnd = end;
                lastCompleted = null;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    pendingName = reader.GetString();
                    continue;
                }

                if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                {
                    JsoncFrameModel closed = stack.Pop();
                    Flush(comments, pending, closed.Path, JsoncCommentPlacement.Inside);
                    lastCompleted = closed.Path;
                    rootDone |= stack.Count == 0;
                    continue;
                }

                JsoncFrameModel? parent = stack.Count == 0 ? null : stack.Peek();
                string path = PathFor(parent, pendingName);

                Flush(comments, pending, path, JsoncCommentPlacement.Before);
                if (parent is { IsArray: true })
                {
                    parent.Index++;
                }

                pendingName = null;

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    stack.Push(new JsoncFrameModel(path, reader.TokenType == JsonTokenType.StartArray));
                }
                else
                {
                    lastCompleted = path;
                    rootDone |= stack.Count == 0;
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        Flush(comments, pending, Root, JsoncCommentPlacement.After);
        return comments;
    }

    public static string Apply(string json, IReadOnlyList<JsoncCommentModel> comments)
    {
        if (comments.Count == 0)
        {
            return json;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Dictionary<string, JsoncSlotModel> slots;
        try
        {
            slots = ScanSlots(bytes);
        }
        catch (JsonException)
        {
            return json;
        }

        List<JsoncInsertionModel> insertions = [];
        foreach ((string path, JsoncCommentPlacement placement, List<string> texts) in Group(comments))
        {
            if (placement == JsoncCommentPlacement.After)
            {
                insertions.Add(new JsoncInsertionModel(bytes.Length, "\n" + string.Join("\n", texts)));
                continue;
            }

            if (!slots.TryGetValue(path, out JsoncSlotModel? slot))
            {
                continue;
            }

            switch (placement)
            {
                case JsoncCommentPlacement.Before:
                    insertions.Add(new JsoncInsertionModel(
                        slot.MemberStart,
                        string.Join("\n" + slot.Indent, texts) + "\n" + slot.Indent));
                    break;

                case JsoncCommentPlacement.Trailing:
                    insertions.Add(new JsoncInsertionModel(
                        AfterSeparator(bytes, slot.MemberEnd),
                        "  " + string.Join(" ", texts)));
                    break;

                case JsoncCommentPlacement.Inside when slot.IsContainer:
                    string inner = slot.InnerIndent ?? slot.Indent + "  ";
                    string close = HasNewline(bytes, slot.ContentEnd, slot.CloseStart)
                        ? string.Empty
                        : "\n" + slot.Indent;
                    insertions.Add(new JsoncInsertionModel(
                        slot.ContentEnd,
                        "\n" + inner + string.Join("\n" + inner, texts) + close));
                    break;
            }
        }

        if (insertions.Count == 0)
        {
            return json;
        }

        insertions.Sort((left, right) => left.Offset.CompareTo(right.Offset));

        StringBuilder builder = new(json.Length + 64);
        int cursor = 0;
        foreach (JsoncInsertionModel insertion in insertions)
        {
            builder.Append(Encoding.UTF8.GetString(bytes, cursor, insertion.Offset - cursor));
            builder.Append(insertion.Text);
            cursor = insertion.Offset;
        }

        builder.Append(Encoding.UTF8.GetString(bytes, cursor, bytes.Length - cursor));
        return builder.ToString();
    }

    private static List<(string Path, JsoncCommentPlacement Placement, List<string> Texts)> Group(
        IReadOnlyList<JsoncCommentModel> comments)
    {
        List<(string, JsoncCommentPlacement, List<string>)> ordered = [];
        Dictionary<(string, JsoncCommentPlacement), List<string>> index = [];

        foreach (JsoncCommentModel comment in comments)
        {
            (string, JsoncCommentPlacement) key = (comment.Path, comment.Placement);
            if (!index.TryGetValue(key, out List<string>? texts))
            {
                texts = [];
                index[key] = texts;
                ordered.Add((comment.Path, comment.Placement, texts));
            }

            texts.Add(comment.Text);
        }

        return ordered;
    }

    private static Dictionary<string, JsoncSlotModel> ScanSlots(byte[] bytes)
    {
        Dictionary<string, JsoncSlotModel> slots = [];
        Stack<JsoncFrameModel> stack = new();
        int pendingStart = -1;
        string? pendingName = null;
        int previousEnd = 0;

        Utf8JsonReader reader = new(bytes, ReaderOptions);
        while (reader.Read())
        {
            int start = (int)reader.TokenStartIndex;
            int end = (int)reader.BytesConsumed;

            if (reader.TokenType == JsonTokenType.Comment)
            {
                previousEnd = end;
                continue;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                pendingName = reader.GetString();
                pendingStart = start;
                previousEnd = end;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                JsoncFrameModel closed = stack.Pop();
                JsoncSlotModel container = slots[closed.Path];
                container.ContentEnd = previousEnd;
                container.CloseStart = start;
                container.MemberEnd = end;
                container.InnerIndent = closed.InnerIndent;
                previousEnd = end;
                continue;
            }

            JsoncFrameModel? parent = stack.Count == 0 ? null : stack.Peek();
            string path = PathFor(parent, pendingName);

            int memberStart = pendingStart >= 0 ? pendingStart : start;
            JsoncSlotModel slot = new()
            {
                MemberStart = memberStart,
                MemberEnd = end,
                Indent = IndentAt(bytes, memberStart)
            };
            slots[path] = slot;

            if (parent is not null)
            {
                parent.InnerIndent ??= slot.Indent;
                if (parent.IsArray)
                {
                    parent.Index++;
                }
            }

            pendingName = null;
            pendingStart = -1;
            previousEnd = end;

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                slot.IsContainer = true;
                stack.Push(new JsoncFrameModel(path, reader.TokenType == JsonTokenType.StartArray));
            }
        }

        return slots;
    }

    private static string PathFor(JsoncFrameModel? parent, string? pendingName) =>
        parent is null
            ? Root
            : parent.IsArray
                ? parent.Path + IndexMark + parent.Index.ToString(CultureInfo.InvariantCulture)
                : parent.Path + MemberMark + (pendingName ?? string.Empty);

    private static void Flush(
        List<JsoncCommentModel> into, List<string> pending, string path, JsoncCommentPlacement placement)
    {
        foreach (string text in pending)
        {
            into.Add(new JsoncCommentModel(path, placement, text));
        }

        pending.Clear();
    }

    private static int AfterSeparator(byte[] bytes, int offset)
    {
        int index = offset;
        while (index < bytes.Length && bytes[index] is (byte)' ' or (byte)'\t')
        {
            index++;
        }

        return index < bytes.Length && bytes[index] == (byte)',' ? index + 1 : offset;
    }

    private static string IndentAt(byte[] bytes, int offset)
    {
        int index = offset;
        while (index > 0 && bytes[index - 1] is (byte)' ' or (byte)'\t')
        {
            index--;
        }

        return Encoding.ASCII.GetString(bytes, index, offset - index);
    }

    private static bool HasNewline(byte[] bytes, int from, int to)
    {
        for (int index = Math.Max(from, 0); index < to && index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\n')
            {
                return true;
            }
        }

        return false;
    }
}
