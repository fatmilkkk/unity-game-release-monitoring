/// <summary>Сравнение версий формата «major.minor.patch» (1.2.0 &lt; 1.10.0).</summary>
public static class VersionUtil
{
    /// <summary>-1, если a &lt; b; 0, если равны; 1, если a &gt; b. Нечисловые части считаются нулём.</summary>
    public static int Compare(string a, string b)
    {
        int[] x = Parse(a);
        int[] y = Parse(b);
        for (int i = 0; i < 3; i++)
        {
            if (x[i] < y[i]) return -1;
            if (x[i] > y[i]) return 1;
        }
        return 0;
    }

    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    private static int[] Parse(string version)
    {
        var result = new int[3];
        if (string.IsNullOrEmpty(version))
            return result;

        string clean = version.Trim().TrimStart('v', 'V');
        int dash = clean.IndexOfAny(new[] { '-', '+', ' ' });
        if (dash >= 0)
            clean = clean.Substring(0, dash);

        string[] parts = clean.Split('.');
        for (int i = 0; i < 3 && i < parts.Length; i++)
            int.TryParse(parts[i], out result[i]);
        return result;
    }
}
