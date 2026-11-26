namespace BezierCore;

/// <summary>
/// Algoritmo de De Casteljau: interpolaciones lineales repetidas
///
///     P_i^(0) = P_i
///     P_i^(r) = (1 - t)·P_i^(r-1) + t·P_{i+1}^(r-1),   r = 1..n,  i = 0..n-r
///     B(t)    = P_0^(n)
///
/// Coste por punto evaluado: n(n+1)/2 interpolaciones → O(n²).
/// Solo usa combinaciones convexas (pesos 1-t y t en [0,1]), lo que lo hace numéricamente robusto.
/// </summary>
public static class DeCasteljau
{
    /// <summary>Evalúa B(t) usando un búfer de trabajo de tamaño ≥ P.Count (evita asignaciones).</summary>
    public static PointD Evaluate(IReadOnlyList<PointD> p, double t, PointD[] buffer)
    {
        int count = p.Count;
        for (int i = 0; i < count; i++) buffer[i] = p[i];

        double u = 1.0 - t;
        for (int r = 1; r < count; r++)
        {
            int last = count - r;
            for (int i = 0; i < last; i++)
            {
                PointD a = buffer[i], b = buffer[i + 1];
                buffer[i] = new PointD(u * a.X + t * b.X, u * a.Y + t * b.Y);
            }
        }
        return buffer[0];
    }

    public static PointD Evaluate(IReadOnlyList<PointD> p, double t)
        => Evaluate(p, t, new PointD[p.Count]);

    public static PointD[] EvaluateCurve(IReadOnlyList<PointD> p, int samples)
    {
        var result = new PointD[samples];
        EvaluateCurve(p, result);
        return result;
    }

    public static void EvaluateCurve(IReadOnlyList<PointD> p, PointD[] destination)
    {
        if (p.Count == 0) return;
        var buffer = new PointD[p.Count];
        int m = destination.Length;
        for (int k = 0; k < m; k++)
            destination[k] = Evaluate(p, Sampling.T(k, m), buffer);
    }

    /// <summary>
    /// Devuelve todos los niveles de interpolación para un t dado:
    /// nivel 0 = puntos de control, nivel n = punto de la curva.
    /// Se usa para visualizar la construcción geométrica.
    /// </summary>
    public static List<PointD[]> Levels(IReadOnlyList<PointD> p, double t)
    {
        var levels = new List<PointD[]>();
        if (p.Count == 0) return levels;
        var current = p.ToArray();
        levels.Add(current);
        while (current.Length > 1)
        {
            var next = new PointD[current.Length - 1];
            for (int i = 0; i < next.Length; i++)
                next[i] = PointD.Lerp(current[i], current[i + 1], t);
            levels.Add(next);
            current = next;
        }
        return levels;
    }

    /// <summary>Subdivide la curva en t: devuelve los polígonos de control de las dos mitades.</summary>
    public static (PointD[] Left, PointD[] Right) Subdivide(IReadOnlyList<PointD> p, double t)
    {
        var levels = Levels(p, t);
        int n = levels.Count;
        var left = new PointD[n];
        var right = new PointD[n];
        for (int r = 0; r < n; r++)
        {
            left[r] = levels[r][0];
            right[n - 1 - r] = levels[r][^1];
        }
        return (left, right);
    }

    /// <summary>Versión en precisión simple (solo para el análisis de estabilidad).</summary>
    public static PointF32 EvaluateF(IReadOnlyList<PointF32> p, float t, PointF32[] buffer)
    {
        int count = p.Count;
        for (int i = 0; i < count; i++) buffer[i] = p[i];
        float u = 1f - t;
        for (int r = 1; r < count; r++)
        {
            int last = count - r;
            for (int i = 0; i < last; i++)
            {
                PointF32 a = buffer[i], b = buffer[i + 1];
                buffer[i] = new PointF32(u * a.X + t * b.X, u * a.Y + t * b.Y);
            }
        }
        return buffer[0];
    }
}
