namespace BezierCore;

/// <summary>
/// Punto 2D en doble precisión. Se usa un tipo propio (y no System.Drawing.PointF)
/// para que los algoritmos matemáticos sean independientes de cualquier API gráfica.
/// </summary>
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public static PointD operator *(double s, PointD p) => new(s * p.X, s * p.Y);

    /// <summary>Interpolación lineal (1 - t)·a + t·b.</summary>
    public static PointD Lerp(PointD a, PointD b, double t)
    {
        double u = 1.0 - t;
        return new PointD(u * a.X + t * b.X, u * a.Y + t * b.Y);
    }

    public double DistanceTo(PointD o)
    {
        double dx = X - o.X, dy = Y - o.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString() => FormattableString.Invariant($"({X:G10}, {Y:G10})");
}

/// <summary>Punto 2D en precisión simple (para el experimento de estabilidad numérica).</summary>
public readonly record struct PointF32(float X, float Y)
{
    public static PointF32 Lerp(PointF32 a, PointF32 b, float t)
    {
        float u = 1f - t;
        return new PointF32(u * a.X + t * b.X, u * a.Y + t * b.Y);
    }

    public PointD ToDouble() => new(X, Y);
}

public static class Sampling
{
    /// <summary>Parámetro t_k = k / (m - 1), k = 0..m-1 (muestras uniformes en [0,1]).</summary>
    public static double T(int k, int samples) => samples <= 1 ? 0.0 : (double)k / (samples - 1);

    public static float TF(int k, int samples) => samples <= 1 ? 0f : (float)k / (samples - 1);
}
