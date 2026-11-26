using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using BezierBenchmark;
using BezierCore;

// ============================================================================
//  Banco de pruebas: Bernstein (evaluación directa) vs. De Casteljau
//  Uso: dotnet run -c Release --project src/BezierBenchmark -- [carpetaSalida] [repeticiones]
//  Solo se cronometra el cálculo de puntos (no hay ninguna representación gráfica).
// ============================================================================

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string outDir = args.Length > 0 ? args[0] : "resultados";
int reps = args.Length > 1 ? Math.Max(10, int.Parse(args[1])) : 10;
Directory.CreateDirectory(outDir);

var md = new StringBuilder();
void Out(string line = "") { Console.WriteLine(line); md.AppendLine(line); }

#if DEBUG
Console.WriteLine("ADVERTENCIA: compilación Debug. Ejecute con -c Release para mediciones válidas.");
#endif

Out("# Resultados experimentales — Bernstein vs. De Casteljau");
Out();
Out($"- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}");
Out($"- Runtime: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}");
Out($"- Procesadores lógicos: {Environment.ProcessorCount}");
Out($"- Stopwatch: alta resolución = {Stopwatch.IsHighResolution}, frecuencia = {Stopwatch.Frequency:N0} Hz");
Out($"- Repeticiones por prueba: {reps} (más 5 corridas de calentamiento no contabilizadas)");
Out($"- Puntos de control aleatorios con coordenadas enteras en [0, 1000] y semilla fija (reproducible).");
Out();

// ----------------------------------------------------------------------------
// 0. Validación de la cúbica calculada a mano
// ----------------------------------------------------------------------------
{
    Out("## 0. Validación de la cúbica de ejemplo");
    Out();
    var cubic = new List<PointD> { new(0, 0), new(1, 2), new(3, 2), new(4, 0) };
    Out("P0=(0,0), P1=(1,2), P2=(3,2), P3=(4,0)");
    Out();
    Out("| t | b0 | b1 | b2 | b3 | Bernstein | De Casteljau | Niveles De Casteljau |");
    Out("|---|---|---|---|---|---|---|---|");
    var binom = Bernstein.BinomialRow(3);
    foreach (double t in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
    {
        var b = Enumerable.Range(0, 4).Select(i => Bernstein.Basis(3, i, t, binom)).ToArray();
        var pb = Bernstein.Evaluate(cubic, t);
        var pc = DeCasteljau.Evaluate(cubic, t);
        var lv = DeCasteljau.Levels(cubic, t);
        string levels = string.Join(" → ", lv.Skip(1).Select(l => string.Join(" ", l.Select(p => p.ToString()))));
        Out($"| {t} | {b[0]} | {b[1]} | {b[2]} | {b[3]} | {pb} | {pc} | {levels} |");
    }
    Out();
}

// ----------------------------------------------------------------------------
// Utilidades
// ----------------------------------------------------------------------------
(TimingStats B, TimingStats C, DifferenceStats D, TimingStats I) Run(IReadOnlyList<PointD> pts, int m)
{
    var destB = new PointD[m];
    var destC = new PointD[m];
    var destI = new PointD[m];
    var arr = pts.ToArray();
    var tb = Metrics.Measure(() => Bernstein.EvaluateCurve(arr, destB), reps, warmup: 5);
    var tc = Metrics.Measure(() => DeCasteljau.EvaluateCurve(arr, destC), reps, warmup: 5);
    var ti = Metrics.Measure(() => Bernstein.EvaluateCurveIncremental(arr, destI), reps, warmup: 5);
    return (tb, tc, Metrics.Compare(destB, destC), ti);
}

string F(double v, string fmt = "0.0000") => double.IsFinite(v) ? v.ToString(fmt) : (double.IsNaN(v) ? "NaN" : "∞");
string E(double v) => double.IsFinite(v) ? v.ToString("0.00E+00") : (double.IsNaN(v) ? "NaN" : "∞");

var csvTiming = new List<string> { "experimento,grado,muestras,metodo,repeticiones,media_ms,desv_ms,min_ms,max_ms,mediana_ms,ns_por_punto,dif_max,dif_media,corridas_ms" };
void AddTiming(string exp, int n, int m, string method, TimingStats s, DifferenceStats d) =>
    csvTiming.Add(FormattableString.Invariant(
        $"{exp},{n},{m},{method},{s.Repetitions},{s.MeanMs:R},{s.StdDevMs:R},{s.MinMs:R},{s.MaxMs:R},{s.MedianMs:R},{s.MeanMs * 1e6 / m:R},{d.Max:R},{d.Mean:R},{string.Join(';', s.RunsMs.Select(r => r.ToString("R")))}"));

// ----------------------------------------------------------------------------
// A. Tiempo vs. grado (m = 1000)
// ----------------------------------------------------------------------------
{
    const int m = 1000;
    int[] degrees = { 2, 3, 5, 8, 10, 15, 20, 30, 50, 75, 100 };
    Out($"## A. Tiempo de ejecución vs. grado (m = {m} muestras, {reps} repeticiones)");
    Out();
    Out("| Grado n | Bernstein media (ms) | σ | De Casteljau media (ms) | σ | DC / Bernstein | Bernstein incremental (ms) | Δmáx entre métodos |");
    Out("|---:|---:|---:|---:|---:|---:|---:|---:|");
    var tb = new double[degrees.Length];
    var tc = new double[degrees.Length];
    var ti = new double[degrees.Length];
    for (int k = 0; k < degrees.Length; k++)
    {
        int n = degrees[k];
        var pts = Metrics.RandomControlPoints(n + 1, seed: 1000 + n);
        var (b, c, d, inc) = Run(pts, m);
        tb[k] = b.MeanMs; tc[k] = c.MeanMs; ti[k] = inc.MeanMs;
        AddTiming("A_grado", n, m, "Bernstein", b, d);
        AddTiming("A_grado", n, m, "DeCasteljau", c, d);
        AddTiming("A_grado", n, m, "BernsteinIncremental", inc, d);
        Out($"| {n} | {F(b.MeanMs)} | {F(b.StdDevMs)} | {F(c.MeanMs)} | {F(c.StdDevMs)} | {F(c.MeanMs / b.MeanMs, "0.00")}× | {F(inc.MeanMs)} | {E(d.Max)} |");
    }
    Out();
    File.WriteAllText(Path.Combine(outDir, "grafico_tiempo_vs_grado.svg"), SvgChart.Line(
        $"Tiempo vs. grado (m = {m} muestras)", "Grado n", "Tiempo medio (ms)",
        degrees.Select(d => (double)d).ToArray(),
        new[] { new SvgChart.Series("Bernstein (Math.Pow)", tb, "#1e64e6"), new SvgChart.Series("De Casteljau", tc, "#dc1e1e"),
                new SvgChart.Series("Bernstein incremental", ti, "#2e9e44", true) }));
    Out("![Tiempo vs grado](grafico_tiempo_vs_grado.svg)");
    Out();
}

// ----------------------------------------------------------------------------
// B. Tiempo vs. número de muestras (n = 3 y n = 20)
// ----------------------------------------------------------------------------
{
    int[] samples = { 100, 500, 1000, 5000, 10000, 50000, 100000 };
    int[] degrees = { 3, 20 };
    Out($"## B. Tiempo de ejecución vs. número de muestras ({reps} repeticiones)");
    Out();
    Out("| Grado n | Muestras m | Bernstein (ms) | De Casteljau (ms) | Bernstein incremental (ms) | Bernstein ns/punto | De Casteljau ns/punto | DC / Bernstein |");
    Out("|---:|---:|---:|---:|---:|---:|---:|---:|");
    var series = new List<SvgChart.Series>();
    string[] colorsB = { "#1e64e6", "#6aa0ff" }, colorsC = { "#dc1e1e", "#ff8a65" };
    for (int di = 0; di < degrees.Length; di++)
    {
        int n = degrees[di];
        var pts = Metrics.RandomControlPoints(n + 1, seed: 2000 + n);
        var tb = new double[samples.Length];
        var tc = new double[samples.Length];
        for (int k = 0; k < samples.Length; k++)
        {
            int m = samples[k];
            var (b, c, d, inc) = Run(pts, m);
            tb[k] = b.MeanMs; tc[k] = c.MeanMs;
            AddTiming("B_muestras", n, m, "Bernstein", b, d);
            AddTiming("B_muestras", n, m, "DeCasteljau", c, d);
            AddTiming("B_muestras", n, m, "BernsteinIncremental", inc, d);
            Out($"| {n} | {m} | {F(b.MeanMs)} | {F(c.MeanMs)} | {F(inc.MeanMs)} | {F(b.MeanMs * 1e6 / m, "0.0")} | {F(c.MeanMs * 1e6 / m, "0.0")} | {F(c.MeanMs / b.MeanMs, "0.00")}× |");
        }
        series.Add(new SvgChart.Series($"Bernstein n={n}", tb, colorsB[di], di == 1));
        series.Add(new SvgChart.Series($"De Casteljau n={n}", tc, colorsC[di], di == 1));
    }
    Out();
    File.WriteAllText(Path.Combine(outDir, "grafico_tiempo_vs_muestras.svg"), SvgChart.Line(
        "Tiempo vs. número de muestras (escala log-log)", "Muestras m", "Tiempo medio (ms)",
        samples.Select(s => (double)s).ToArray(), series, logX: true, logY: true));
    Out("![Tiempo vs muestras](grafico_tiempo_vs_muestras.svg)");
    Out();
}

// ----------------------------------------------------------------------------
// C. Matriz grado × muestras
// ----------------------------------------------------------------------------
{
    int[] degrees = { 3, 5, 10, 20, 50 };
    int[] samples = { 100, 1000, 10000 };
    Out($"## C. Matriz de pruebas grado × muestras ({reps} repeticiones; media ± σ en ms)");
    Out();
    Out("| Grado n | Muestras m | Bernstein (Math.Pow) | De Casteljau | Más rápido (Pow vs DC) | Bernstein incremental | Δmáx | Δmedia |");
    Out("|---:|---:|---:|---:|:---:|---:|---:|---:|");
    foreach (int n in degrees)
    {
        var pts = Metrics.RandomControlPoints(n + 1, seed: 3000 + n);
        foreach (int m in samples)
        {
            var (b, c, d, inc) = Run(pts, m);
            AddTiming("C_matriz", n, m, "Bernstein", b, d);
            AddTiming("C_matriz", n, m, "DeCasteljau", c, d);
            AddTiming("C_matriz", n, m, "BernsteinIncremental", inc, d);
            string fastest = b.MeanMs < c.MeanMs ? $"Bernstein ({F(c.MeanMs / b.MeanMs, "0.00")}×)" : $"De Casteljau ({F(b.MeanMs / c.MeanMs, "0.00")}×)";
            Out($"| {n} | {m} | {F(b.MeanMs)} ± {F(b.StdDevMs)} | {F(c.MeanMs)} ± {F(c.StdDevMs)} | {fastest} | {F(inc.MeanMs)} ± {F(inc.StdDevMs)} | {E(d.Max)} | {E(d.Mean)} |");
        }
    }
    Out();
}
File.WriteAllLines(Path.Combine(outDir, "tiempos.csv"), csvTiming, Encoding.UTF8);

// ----------------------------------------------------------------------------
// D. Precisión frente a la solución exacta (aritmética racional)
// ----------------------------------------------------------------------------
{
    const int m = 101;
    int[] degrees = { 3, 5, 10, 20, 30, 50, 75, 100 };
    const double u64 = 1.1102230246251565e-16; // unidad de redondeo double (2^-53)
    Out($"## D. Error respecto a la solución exacta (m = {m} muestras, coordenadas en [0,1000])");
    Out();
    Out("Error = distancia euclidiana entre el punto calculado y el valor exacto obtenido con aritmética racional (BigInteger).");
    Out();
    Out("| Grado n | Bernstein double máx | Bernstein incremental double máx | De Casteljau double máx | Bernstein double medio | De Casteljau double medio | Bernstein float máx | De Casteljau float máx |");
    Out("|---:|---:|---:|---:|---:|---:|---:|---:|");
    var csv = new List<string> { "grado,muestras,bernstein_d_max,decasteljau_d_max,bernstein_d_media,decasteljau_d_media,bernstein_f_max,decasteljau_f_max,bernstein_f_media,decasteljau_f_media" };
    var eBd = new double[degrees.Length]; var eCd = new double[degrees.Length];
    var eBf = new double[degrees.Length]; var eCf = new double[degrees.Length];
    for (int k = 0; k < degrees.Length; k++)
    {
        int n = degrees[k];
        var pts = Metrics.RandomControlPoints(n + 1, seed: 4000 + n);
        var ptsF = pts.Select(p => new PointF32((float)p.X, (float)p.Y)).ToArray();
        var binom = Bernstein.BinomialRow(n);
        var binomF = Bernstein.BinomialRowF(n);
        var buf = new PointD[n + 1];
        var bufF = new PointF32[n + 1];
        var powT = new double[n + 1]; var powU = new double[n + 1];
        double maxId = 0;
        double maxBd = 0, maxCd = 0, maxBf = 0, maxCf = 0, sumBd = 0, sumCd = 0, sumBf = 0, sumCf = 0;
        for (int i = 0; i < m; i++)
        {
            double t = Sampling.T(i, m);
            float tf = Sampling.TF(i, m);
            double bd = ExactReference.Error(pts, t, Bernstein.Evaluate(pts, t, binom)).Magnitude;
            double cd = ExactReference.Error(pts, t, DeCasteljau.Evaluate(pts, t, buf)).Magnitude;
            maxId = Math.Max(maxId, ExactReference.Error(pts, t, Bernstein.EvaluateIncremental(pts, t, binom, powT, powU)).Magnitude);
            double bf = ExactReference.Error(pts, tf, Bernstein.EvaluateF(ptsF, tf, binomF).ToDouble()).Magnitude;
            double cf = ExactReference.Error(pts, tf, DeCasteljau.EvaluateF(ptsF, tf, bufF).ToDouble()).Magnitude;
            maxBd = Math.Max(maxBd, bd); maxCd = Math.Max(maxCd, cd); maxBf = Math.Max(maxBf, bf); maxCf = Math.Max(maxCf, cf);
            sumBd += bd; sumCd += cd; sumBf += bf; sumCf += cf;
        }
        eBd[k] = maxBd; eCd[k] = maxCd; eBf[k] = maxBf; eCf[k] = maxCf;
        csv.Add(FormattableString.Invariant($"{n},{m},{maxBd:R},{maxCd:R},{sumBd / m:R},{sumCd / m:R},{maxBf:R},{maxCf:R},{sumBf / m:R},{sumCf / m:R}"));
        Out($"| {n} | {E(maxBd)} | {E(maxId)} | {E(maxCd)} | {E(sumBd / m)} | {E(sumCd / m)} | {E(maxBf)} | {E(maxCf)} |");
    }
    Out();
    Out($"Referencia: unidad de redondeo double u = {u64:0.00E+00} (× 1000 = {u64 * 1000:0.00E+00}); float u = {Math.Pow(2, -24):0.00E+00} (× 1000 = {Math.Pow(2, -24) * 1000:0.00E+00}).");
    Out();
    File.WriteAllLines(Path.Combine(outDir, "precision.csv"), csv, Encoding.UTF8);
    File.WriteAllText(Path.Combine(outDir, "grafico_error_vs_grado.svg"), SvgChart.Line(
        "Error máximo vs. solución exacta (escala log)", "Grado n", "Error absoluto máximo",
        degrees.Select(d => (double)d).ToArray(),
        new[]
        {
            new SvgChart.Series("Bernstein double", eBd, "#1e64e6"),
            new SvgChart.Series("De Casteljau double", eCd, "#dc1e1e"),
            new SvgChart.Series("Bernstein float", eBf, "#1e64e6", true),
            new SvgChart.Series("De Casteljau float", eCf, "#dc1e1e", true),
        }, logY: true));
    Out("![Error vs grado](grafico_error_vs_grado.svg)");
    Out();
}

// ----------------------------------------------------------------------------
// E. Grados extremos: desbordamiento de C(n,i)
// ----------------------------------------------------------------------------
{
    const int m = 201;
    Out($"## E. Grados extremos: desbordamiento de los coeficientes binomiales (m = {m})");
    Out();
    Out("### E.1 Doble precisión");
    Out();
    Out("| Grado n | máx C(n,i) | Puntos no finitos Bernstein | Puntos no finitos De Casteljau | Δmáx (puntos finitos) |");
    Out("|---:|---:|---:|---:|---:|");
    var csv = new List<string> { "precision,grado,max_binomial,no_finitos_bernstein,no_finitos_decasteljau,dif_max" };
    foreach (int n in new[] { 100, 500, 1000, 1020, 1028, 1029, 1030, 1050, 1100 })
    {
        var pts = Metrics.RandomControlPoints(n + 1, seed: 5000 + n);
        var b = Bernstein.EvaluateCurve(pts, m);
        var c = DeCasteljau.EvaluateCurve(pts, m);
        int nb = b.Count(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y));
        int nc = c.Count(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y));
        double maxBin = Bernstein.BinomialRow(n).Max();
        var d = Metrics.Compare(b, c);
        csv.Add(FormattableString.Invariant($"double,{n},{maxBin:R},{nb},{nc},{d.Max:R}"));
        Out($"| {n} | {E(maxBin)} | {nb} / {m} | {nc} / {m} | {(d.InvalidPoints == m ? "— (sin puntos válidos)" : E(d.Max))} |");
    }
    Out();
    Out("### E.2 Precisión simple (float)");
    Out();
    Out("| Grado n | máx C(n,i) | Puntos no finitos Bernstein | Puntos no finitos De Casteljau |");
    Out("|---:|---:|---:|---:|");
    foreach (int n in new[] { 50, 100, 120, 125, 128, 129, 130, 135, 150 })
    {
        var pts = Metrics.RandomControlPoints(n + 1, seed: 6000 + n).Select(p => new PointF32((float)p.X, (float)p.Y)).ToArray();
        var binomF = Bernstein.BinomialRowF(n);
        var buf = new PointF32[n + 1];
        int nb = 0, nc = 0;
        for (int i = 0; i < m; i++)
        {
            float t = Sampling.TF(i, m);
            var pb = Bernstein.EvaluateF(pts, t, binomF);
            var pc = DeCasteljau.EvaluateF(pts, t, buf);
            if (!float.IsFinite(pb.X) || !float.IsFinite(pb.Y)) nb++;
            if (!float.IsFinite(pc.X) || !float.IsFinite(pc.Y)) nc++;
        }
        csv.Add(FormattableString.Invariant($"float,{n},{binomF.Max():R},{nb},{nc},"));
        Out($"| {n} | {E(binomF.Max())} | {nb} / {m} | {nc} / {m} |");
    }
    Out();
    File.WriteAllLines(Path.Combine(outDir, "grados_extremos.csv"), csv, Encoding.UTF8);
}

File.WriteAllText(Path.Combine(outDir, "resultados.md"), md.ToString(), Encoding.UTF8);
Console.WriteLine();
Console.WriteLine($"Resultados guardados en: {Path.GetFullPath(outDir)}");
