using System.Numerics;

namespace BezierCore;

/// <summary>
/// Evaluación EXACTA de B(t) con aritmética racional (BigInteger), usada como referencia
/// para medir el error de redondeo de los métodos en punto flotante.
///
/// Todo double es un racional exacto m·2^e. Si t = M / 2^E, cada paso de De Casteljau
///     (2^E - M)·a + M·b   (dividido por 2^E)
/// es una operación entera exacta. Tras n niveles el denominador común es 2^(E·n + S).
/// </summary>
public static class ExactReference
{
    public readonly record struct ErrorPair(double ErrorX, double ErrorY)
    {
        public double Magnitude => Math.Sqrt(ErrorX * ErrorX + ErrorY * ErrorY);
    }

    /// <summary>
    /// Calcula el error |aprox - exacto| para el punto B(t) dado, sin redondear el valor exacto.
    /// </summary>
    public static ErrorPair Error(IReadOnlyList<PointD> p, double t, PointD approx)
    {
        int count = p.Count;

        // Escala común S para que todas las coordenadas sean enteras: coord = entero / 2^S
        int s = 0;
        foreach (var q in p)
        {
            s = Math.Max(s, -Decompose(q.X).Exp);
            s = Math.Max(s, -Decompose(q.Y).Exp);
        }

        var xs = new BigInteger[count];
        var ys = new BigInteger[count];
        for (int i = 0; i < count; i++)
        {
            xs[i] = ToScaledInteger(p[i].X, s);
            ys[i] = ToScaledInteger(p[i].Y, s);
        }

        // t = M / 2^E
        var (mt, et) = Decompose(t);
        BigInteger M;
        int E;
        if (et >= 0) { M = (BigInteger)mt << et; E = 0; }
        else { M = mt; E = -et; }
        BigInteger oneMinus = (BigInteger.One << E) - M;

        for (int r = 1; r < count; r++)
        {
            int last = count - r;
            for (int i = 0; i < last; i++)
            {
                xs[i] = oneMinus * xs[i] + M * xs[i + 1];
                ys[i] = oneMinus * ys[i] + M * ys[i + 1];
            }
        }

        int k = E * (count - 1) + s; // denominador 2^k
        return new ErrorPair(
            Math.Abs(Difference(approx.X, xs[0], k)),
            Math.Abs(Difference(approx.Y, ys[0], k)));
    }

    /// <summary>Devuelve approx - num/2^k calculado exactamente y luego convertido a double.</summary>
    private static double Difference(double approx, BigInteger num, int k)
    {
        if (double.IsNaN(approx) || double.IsInfinity(approx)) return double.PositiveInfinity;
        var (ma, ea) = Decompose(approx);
        int c = Math.Min(-k, ea);
        BigInteger a = (BigInteger)ma << (ea - c);
        BigInteger b = num << (-k - c);
        return ToDouble(a - b, c);
    }

    private static double ToDouble(BigInteger value, int exp2)
    {
        if (value.IsZero) return 0.0;
        int sign = value.Sign;
        BigInteger abs = BigInteger.Abs(value);
        long bits = (long)abs.GetBitLength();
        int shift = (int)Math.Max(0, bits - 62);
        double mantissa = (double)(abs >> shift);
        return sign * Math.ScaleB(mantissa, shift + exp2);
    }

    private static BigInteger ToScaledInteger(double v, int s)
    {
        var (m, e) = Decompose(v);
        return (BigInteger)m << (e + s);
    }

    /// <summary>Descompone v = mantissa · 2^exp de forma exacta.</summary>
    public static (long Mantissa, int Exp) Decompose(double v)
    {
        if (v == 0.0) return (0, 0);
        long bits = BitConverter.DoubleToInt64Bits(v);
        bool negative = bits < 0;
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0) exponent = 1; else mantissa |= 1L << 52;
        exponent -= 1075;
        // Normalizar quitando ceros finales (exponentes más pequeños en magnitud)
        while (mantissa != 0 && (mantissa & 1) == 0) { mantissa >>= 1; exponent++; }
        return (negative ? -mantissa : mantissa, exponent);
    }
}
