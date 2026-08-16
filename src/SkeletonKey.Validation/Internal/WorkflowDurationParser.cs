using System.Globalization;

namespace SkeletonKey.Validation.Internal;

internal static class WorkflowDurationParser
{
    public static bool TryParse(string? text, out TimeSpan duration)
    {
        duration = default;

        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text) || text[0] != 'P')
        {
            return false;
        }

        int index = 1;
        bool any = false;
        decimal totalSeconds = 0;

        if (index < text.Length && char.IsDigit(text[index]))
        {
            if (!TryReadNumber(text, ref index, allowFraction: false, out decimal days) ||
                index >= text.Length ||
                text[index] != 'D')
            {
                return false;
            }

            totalSeconds += days * 86400;
            index++;
            any = true;
        }

        if (index == text.Length)
        {
            return any && TryCreateTimeSpan(totalSeconds, out duration);
        }

        if (text[index] != 'T')
        {
            return false;
        }

        index++;
        bool anyTime = false;
        int previousComponent = 0;

        while (index < text.Length)
        {
            int numberStart = index;
            if (!TryReadNumber(text, ref index, allowFraction: true, out decimal value) || index >= text.Length)
            {
                return false;
            }

            char designator = text[index];
            int component = designator switch
            {
                'H' => 1,
                'M' => 2,
                'S' => 3,
                _ => 0,
            };

            if (component == 0 || component <= previousComponent)
            {
                return false;
            }

            if (designator is 'H' or 'M' && text.AsSpan(numberStart, index - numberStart).Contains('.'))
            {
                return false;
            }

            totalSeconds += designator switch
            {
                'H' => value * 3600,
                'M' => value * 60,
                _ => value,
            };

            previousComponent = component;
            index++;
            any = true;
            anyTime = true;
        }

        return any && anyTime && TryCreateTimeSpan(totalSeconds, out duration);
    }

    private static bool TryReadNumber(string text, ref int index, bool allowFraction, out decimal value)
    {
        value = 0;
        int start = index;

        while (index < text.Length && char.IsDigit(text[index]))
        {
            index++;
        }

        if (allowFraction && index < text.Length && text[index] == '.')
        {
            index++;

            if (index == text.Length || !char.IsDigit(text[index]))
            {
                return false;
            }

            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }
        }

        if (index == start)
        {
            return false;
        }

        return decimal.TryParse(
            text.AsSpan(start, index - start),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryCreateTimeSpan(decimal totalSeconds, out TimeSpan duration)
    {
        duration = default;

        if (totalSeconds < 0)
        {
            return false;
        }

        decimal ticks = totalSeconds * TimeSpan.TicksPerSecond;
        if (ticks > TimeSpan.MaxValue.Ticks)
        {
            return false;
        }

        duration = TimeSpan.FromTicks((long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero));
        return true;
    }
}
