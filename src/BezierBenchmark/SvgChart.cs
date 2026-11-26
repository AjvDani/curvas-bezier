using System.Globalization;
using System.Text;

namespace BezierBenchmark;

/// <summary>Generador mínimo de gráficos de líneas en SVG (sin dependencias externas).</summary>
internal static class SvgChart
{
    public sealed record Series(string Name, double[] Y, string Color, bool Dashed = false);

    public static string Line(string title, string xLabel, string yLabel, double[] xs, IReadOnlyList<Series> series,
                              bool logX = false, bool logY = false)
    {
        const int W = 760, H = 440, ML = 80, MR = 190, MT = 44, MB = 58;
        int pw = W - ML - MR, ph = H - MT - MB;
        var ci = CultureInfo.InvariantCulture;

        var allY = series.SelectMany(s => s.Y).Where(v => double.IsFinite(v) && (!logY || v > 0)).ToArray();
        double xmin = xs.Min(), xmax = xs.Max();
        double ymin = allY.Min(), ymax = allY.Max();
        if (logY) { ymin = Math.Pow(10, Math.Floor(Math.Log10(ymin))); ymax = Math.Pow(10, Math.Ceiling(Math.Log10(ymax))); }
        else { ymin = Math.Min(0, ymin); ymax = NiceCeil(ymax * 1.05); }
        if (logX) { xmin = Math.Pow(10, Math.Floor(Math.Log10(xmin))); xmax = Math.Pow(10, Math.Ceiling(Math.Log10(xmax))); }
        if (ymax <= ymin) ymax = ymin + 1;

        double Fx(double x) => logX ? Math.Log10(x) : x;
        double Fy(double y) => logY ? Math.Log10(y) : y;
        double Px(double x) => ML + (Fx(x) - Fx(xmin)) / (Fx(xmax) - Fx(xmin)) * pw;
        double Py(double y) => MT + ph - (Fy(y) - Fy(ymin)) / (Fy(ymax) - Fy(ymin)) * ph;

        var sb = new StringBuilder();
        sb.Append(ci, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W}\" height=\"{H}\" viewBox=\"0 0 {W} {H}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"12\">\n");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
        sb.Append(ci, $"<text x=\"{W / 2 - MR / 2}\" y=\"24\" text-anchor=\"middle\" font-size=\"15\" font-weight=\"bold\">{Esc(title)}</text>\n");

        // Ejes y rejilla
        foreach (double ty in Ticks(ymin, ymax, logY))
        {
            double y = Py(ty);
            sb.Append(ci, $"<line x1=\"{ML}\" y1=\"{y:0.##}\" x2=\"{ML + pw}\" y2=\"{y:0.##}\" stroke=\"#e3e3e3\"/>\n");
            sb.Append(ci, $"<text x=\"{ML - 6}\" y=\"{y + 4:0.##}\" text-anchor=\"end\">{Fmt(ty, logY)}</text>\n");
        }
        foreach (double tx in logX ? Ticks(xmin, xmax, true) : xs)
        {
            double x = Px(tx);
            sb.Append(ci, $"<line x1=\"{x:0.##}\" y1=\"{MT}\" x2=\"{x:0.##}\" y2=\"{MT + ph}\" stroke=\"#f0f0f0\"/>\n");
            sb.Append(ci, $"<text x=\"{x:0.##}\" y=\"{MT + ph + 16}\" text-anchor=\"middle\">{Fmt(tx, logX)}</text>\n");
        }
        sb.Append(ci, $"<rect x=\"{ML}\" y=\"{MT}\" width=\"{pw}\" height=\"{ph}\" fill=\"none\" stroke=\"#555\"/>\n");
        sb.Append(ci, $"<text x=\"{ML + pw / 2}\" y=\"{H - 14}\" text-anchor=\"middle\">{Esc(xLabel)}</text>\n");
        sb.Append(ci, $"<text transform=\"translate(18 {MT + ph / 2}) rotate(-90)\" text-anchor=\"middle\">{Esc(yLabel)}</text>\n");

        // Series
        int li = 0;
        foreach (var s in series)
        {
            var pts = new List<string>();
            for (int i = 0; i < xs.Length && i < s.Y.Length; i++)
                if (double.IsFinite(s.Y[i]) && (!logY || s.Y[i] > 0))
                    pts.Add(string.Format(ci, "{0:0.##},{1:0.##}", Px(xs[i]), Py(s.Y[i])));
            string dash = s.Dashed ? " stroke-dasharray=\"6 4\"" : "";
            sb.Append(ci, $"<polyline points=\"{string.Join(' ', pts)}\" fill=\"none\" stroke=\"{s.Color}\" stroke-width=\"2.2\"{dash}/>\n");
            foreach (var p in pts)
            {
                var xy = p.Split(',');
                sb.Append(ci, $"<circle cx=\"{xy[0]}\" cy=\"{xy[1]}\" r=\"3.2\" fill=\"{s.Color}\"/>\n");
            }
            int ly = MT + 10 + li * 22;
            sb.Append(ci, $"<line x1=\"{ML + pw + 14}\" y1=\"{ly}\" x2=\"{ML + pw + 40}\" y2=\"{ly}\" stroke=\"{s.Color}\" stroke-width=\"2.5\"{dash}/>\n");
            sb.Append(ci, $"<text x=\"{ML + pw + 46}\" y=\"{ly + 4}\">{Esc(s.Name)}</text>\n");
            li++;
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static IEnumerable<double> Ticks(double min, double max, bool log)
    {
        if (log)
        {
            for (int e = (int)Math.Round(Math.Log10(min)); e <= (int)Math.Round(Math.Log10(max)); e++)
                yield return Math.Pow(10, e);
            yield break;
        }
        double step = NiceCeil((max - min) / 6);
        for (double v = min; v <= max + step * 1e-9; v += step) yield return v;
    }

    private static double NiceCeil(double v)
    {
        if (v <= 0) return 1;
        double e = Math.Pow(10, Math.Floor(Math.Log10(v)));
        double f = v / e;
        double nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
        return nf * e;
    }

    private static string Fmt(double v, bool log) =>
        log ? (Math.Abs(Math.Log10(v)) >= 4 ? $"1e{Math.Round(Math.Log10(v))}" : v.ToString("0.####", CultureInfo.InvariantCulture))
            : v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
