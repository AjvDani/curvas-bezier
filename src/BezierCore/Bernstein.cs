namespace BezierCore;

/// <summary>
/// Evaluación directa de una curva de Bézier mediante la base de polinomios de Bernstein:
///
///     B(t) = Σ_{i=0..n} C(n,i) · t^i · (1 - t)^(n-i) · P_i
///
/// Coste por punto evaluado: O(n) términos (cada uno con dos potencias).
/// Los coeficientes binomiales se precalculan una sola vez por curva (O(n²) con Pascal, despreciable).
/// </summary>
public static class Bernstein
{
    /// <summary>Fila n del triángulo de Pascal: C(n,0..n). Exacta en double mientras C(n,i) &lt; 2^53.</summary>
    public static double[] BinomialRow(int n)
    {
        var row = new double[n + 1];
        row[0] = 1.0;
        for (int k = 1; k <= n; k++)
        {
            row[k] = 1.0;
            for (int i = k - 1; i >= 1; i--)
                row[i] += row[i - 1];
        }
        return row;
    }

    public static float[] BinomialRowF(int n)
    {
        var row = new float[n + 1];
        row[0] = 1f;
        for (int k = 1; k <= n; k++)
        {
            row[k] = 1f;
            for (int i = k - 1; i >= 1; i--)
                row[i] += row[i - 1];
        }
        return row;
    }

    /// <summary>Polinomio base de Bernstein b_{i,n}(t).</summary>
    public static double Basis(int n, int i, double t, double[] binomial)
        => binomial[i] * Math.Pow(t, i) * Math.Pow(1.0 - t, n - i);

    /// <summary>Evalúa B(t) para un único t (requiere la fila binomial del grado n = P.Count - 1).</summary>
    public static PointD Evaluate(IReadOnlyList<PointD> p, double t, double[] binomial)
    {
        int n = p.Count - 1;
        double u = 1.0 - t;
        double x = 0.0, y = 0.0;
        for (int i = 0; i <= n; i++)
        {
            double b = binomial[i] * Math.Pow(t, i) * Math.Pow(u, n - i);
            x += b * p[i].X;
            y += b * p[i].Y;
        }
        return new PointD(x, y);
    }

    public static PointD Evaluate(IReadOnlyList<PointD> p, double t)
        => Evaluate(p, t, BinomialRow(p.Count - 1));

    /// <summary>Genera <paramref name="samples"/> puntos de la curva con t uniforme en [0,1].</summary>
    public static PointD[] EvaluateCurve(IReadOnlyList<PointD> p, int samples)
    {
        var result = new PointD[samples];
        EvaluateCurve(p, result);
        return result;
    }

    /// <summary>Versión sin asignaciones (el arreglo destino se reutiliza en las mediciones).</summary>
    public static void EvaluateCurve(IReadOnlyList<PointD> p, PointD[] destination)
    {
        if (p.Count == 0) return;
        double[] binomial = BinomialRow(p.Count - 1);
        int m = destination.Length;
        for (int k = 0; k < m; k++)
            destination[k] = Evaluate(p, Sampling.T(k, m), binomial);
    }

    // ------------------------------------------------------------------
    // Variante optimizada: potencias incrementales (sin Math.Pow).
    // t^i y (1-t)^(n-i) se obtienen por multiplicaciones sucesivas: ~3n
    // multiplicaciones + 2(n+1) multiplicaciones-suma por punto. Sigue siendo O(n).
    // ------------------------------------------------------------------
    public static PointD EvaluateIncremental(IReadOnlyList<PointD> p, double t, double[] binomial, double[] powT, double[] powU)
    {
        int n = p.Count - 1;
        double u = 1.0 - t;
        powT[0] = 1.0;
        powU[0] = 1.0;
        for (int i = 1; i <= n; i++)
        {
            powT[i] = powT[i - 1] * t;
            powU[i] = powU[i - 1] * u;
        }
        double x = 0.0, y = 0.0;
        for (int i = 0; i <= n; i++)
        {
            double b = binomial[i] * powT[i] * powU[n - i];
            x += b * p[i].X;
            y += b * p[i].Y;
        }
        return new PointD(x, y);
    }

    public static void EvaluateCurveIncremental(IReadOnlyList<PointD> p, PointD[] destination)
    {
        if (p.Count == 0) return;
        int n = p.Count - 1;
        double[] binomial = BinomialRow(n);
        var powT = new double[n + 1];
        var powU = new double[n + 1];
        int m = destination.Length;
        for (int k = 0; k < m; k++)
            destination[k] = EvaluateIncremental(p, Sampling.T(k, m), binomial, powT, powU);
    }

    /// <summary>Versión en precisión simple (solo para el análisis de estabilidad).</summary>
    public static PointF32 EvaluateF(IReadOnlyList<PointF32> p, float t, float[] binomial)
    {
        int n = p.Count - 1;
        float u = 1f - t;
        float x = 0f, y = 0f;
        for (int i = 0; i <= n; i++)
        {
            float b = binomial[i] * MathF.Pow(t, i) * MathF.Pow(u, n - i);
            x += b * p[i].X;
            y += b * p[i].Y;
        }
        return new PointF32(x, y);
    }
}
