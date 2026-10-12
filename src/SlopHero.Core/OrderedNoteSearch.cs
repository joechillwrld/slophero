namespace SlopHero.Core;

public static class OrderedNoteSearch
{
    public static int LowerBound(int count, long startTick, Func<int, long> tickAt)
    {
        int left = 0, right = count;
        while (left < right)
        {
            int middle = left + (right - left) / 2;
            if (tickAt(middle) < startTick) left = middle + 1;
            else right = middle;
        }
        return left;
    }
}
