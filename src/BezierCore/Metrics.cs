using System.Diagnostics;

namespace BezierCore;

public sealed record TimingStats(double MeanMs, double StdDevMs, double MinMs, double MaxMs, double MedianMs, double[] RunsMs)
{
    public int Repetitions => RunsMs.Length;
}

public sealed record DifferenceStats(double Max, double Mean, double Rms, int IndexOfMax, int InvalidPoints);

public static class Metrics
{
    /// <summary>
    /// Mide <paramref name="action"/> con Stopwatch, <paramref name="repetitions"/> veces.
    /// Se ejecutan corridas de calentamiento (JIT) que no se contabilizan.
    /// Solo se mide el cálculo: la acción NO debe dibujar nada.
    /// </summary>
    public static TimingStats Measure(Action action, int repetitions, int warmup = 2)
    {
        if (repetitions < 1) throw new ArgumentOutOfRangeException(nameof(repetitions));
        for (int i = 0; i < warmup; i++) action();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var runs = new double[repetitions];
        var sw = new Stopwatch();
        for (int i = 0; i < repetitions; i++)
        {
            sw.Restart();
            action();
            sw.Stop();
            runs[i] = sw.Elapsed.TotalMilliseconds;
        }

        double mean = runs.Average();
        double variance = runs.Length > 1 ? runs.Sum(r => (r - mean) * (r - mean)) / (runs.Length - 1) : 0.0;
        var sorted = runs.OrderBy(r => r).ToArray();
        double median = sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : 0.5 * (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]);

        return new TimingStats(mean, Math.Sqrt(variance), sorted[0], sorted[^1], median, runs);
    }

    /// <summary>Distancia euclidiana punto a punto entre dos muestreos de la misma curva.</summary>
    public static DifferenceStats Compare(IReadOnlyList<PointD> a, IReadOnlyList<PointD> b)
    {
        int n = Math.Min(a.Count, b.Count);
        double max = 0, sum = 0, sumSq = 0;
        int idx = -1, invalid = 0, valid = 0;
        for (int i = 0; i < n; i++)
        {
            double d = a[i].DistanceTo(b[i]);
            if (!double.IsFinite(d)) { invalid++; continue; }
            valid++;
            sum += d;
            sumSq += d * d;
            if (d > max || idx < 0) { max = d; idx = i; }
        }
        return valid == 0
            ? new DifferenceStats(double.NaN, double.NaN, double.NaN, -1, invalid)
            : new DifferenceStats(max, sum / valid, Math.Sqrt(sumSq / valid), idx, invalid);
    }

    /// <summary>Puntos de control aleatorios reproducibles en [0, width]×[0, height].</summary>
    public static List<PointD> RandomControlPoints(int count, int seed, double width = 1000, double height = 1000, bool integer = true)
    {
        var rnd = new Random(seed);
        var list = new List<PointD>(count);
        for (int i = 0; i < count; i++)
        {
            double x = rnd.NextDouble() * width, y = rnd.NextDouble() * height;
            if (integer) { x = Math.Round(x); y = Math.Round(y); }
            list.Add(new PointD(x, y));
        }
        return list;
    }
}
