using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using BezierCore;

namespace BezierApp;

/// <summary>Panel con doble búfer para dibujar sin parpadeo.</summary>
internal sealed class CanvasPanel : Panel
{
    public CanvasPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
    }
}

public sealed class MainForm : Form
{
    private const float HitRadius = 9f;

    // --- Estado del modelo ---
    private readonly List<PointD> _points = new();
    private PointD[] _bernstein = Array.Empty<PointD>();
    private PointD[] _casteljau = Array.Empty<PointD>();
    private int _dragIndex = -1;
    private bool _busy;
    private bool _suppressGrid;
    private readonly List<string> _csvRows = new();

    // --- Controles ---
    private readonly CanvasPanel _canvas = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _numSamples = new() { Minimum = 2, Maximum = 200000, Value = 200, Width = 100, ThousandsSeparator = true };
    private readonly TrackBar _trackT = new() { Minimum = 0, Maximum = 1000, Value = 500, TickFrequency = 100, Width = 330 };
    private readonly Label _lblT = new() { AutoSize = true };
    private readonly CheckBox _chkBernstein = new() { Text = "Curva por Bernstein (azul)", Checked = true, AutoSize = true };
    private readonly CheckBox _chkCasteljau = new() { Text = "Curva por De Casteljau (rojo)", Checked = true, AutoSize = true };
    private readonly CheckBox _chkPolygon = new() { Text = "Polígono de control", Checked = true, AutoSize = true };
    private readonly CheckBox _chkConstruction = new() { Text = "Interpolaciones de De Casteljau en t", Checked = true, AutoSize = true };
    private readonly CheckBox _chkSamples = new() { Text = "Marcar puntos muestreados", Checked = false, AutoSize = true };
    private readonly NumericUpDown _numDegree = new() { Minimum = 1, Maximum = 100, Value = 5, Width = 60 };
    private readonly DataGridView _grid = new();
    private readonly Label _lblInfo = new() { AutoSize = false, Width = 345, Height = 110, Font = new Font("Consolas", 8.5f) };
    private readonly NumericUpDown _numReps = new() { Minimum = 10, Maximum = 10000, Value = 10, Width = 70 };
    private readonly TextBox _txtLog = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Width = 345, Height = 200, Font = new Font("Consolas", 8.5f) };
    private readonly Button _btnMeasure = new() { Text = "Medir curva actual", AutoSize = true };
    private readonly Button _btnSweep = new() { Text = "Experimento grado × muestras", AutoSize = true };

    public MainForm()
    {
        Text = "Curvas de Bézier — Bernstein vs. De Casteljau";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        WireEvents();
        LoadExampleCubic();
    }

    // ===================================================================== UI
    private void BuildLayout()
    {
        var side = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 400,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
            BackColor = Color.FromArgb(245, 246, 248)
        };

        // Puntos de control
        var gbPoints = Group("Puntos de control");
        var rowGen = Row();
        var btnRandom = new Button { Text = "Curva aleatoria de grado", AutoSize = true };
        btnRandom.Click += (_, _) => GenerateRandom((int)_numDegree.Value);
        rowGen.Controls.AddRange(new Control[] { btnRandom, _numDegree });
        var rowBtns = Row();
        var btnExample = new Button { Text = "Cúbica de validación", AutoSize = true };
        btnExample.Click += (_, _) => LoadExampleCubic();
        var btnDelete = new Button { Text = "Eliminar seleccionado", AutoSize = true };
        btnDelete.Click += (_, _) => DeleteSelectedRow();
        var btnClear = new Button { Text = "Limpiar", AutoSize = true };
        btnClear.Click += (_, _) => { _points.Clear(); PointsChanged(); };
        rowBtns.Controls.AddRange(new Control[] { btnExample, btnDelete, btnClear });

        _grid.Width = 345;
        _grid.Height = 170;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add("i", "P_i");
        _grid.Columns.Add("x", "X");
        _grid.Columns.Add("y", "Y");
        _grid.Columns[0].ReadOnly = true;
        _grid.Columns[0].FillWeight = 30;

        var hint = new Label
        {
            AutoSize = false, Width = 345, Height = 34, ForeColor = Color.DimGray,
            Text = "Lienzo: clic izq. = agregar · arrastrar = mover · clic der. = eliminar. " +
                   "Las coordenadas también se editan en la tabla."
        };
        AddToGroup(gbPoints, rowGen, rowBtns, _grid, hint);

        // Muestreo y visualización
        var gbView = Group("Muestreo y visualización");
        var rowSamples = Row();
        rowSamples.Controls.Add(new Label { Text = "Muestras (m):", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        rowSamples.Controls.Add(_numSamples);
        var rowT = Row();
        rowT.Controls.Add(_lblT);
        AddToGroup(gbView, rowSamples, rowT, _trackT, _chkBernstein, _chkCasteljau, _chkPolygon, _chkConstruction, _chkSamples, _lblInfo);

        // Medición
        var gbMeasure = Group("Medición de rendimiento (Stopwatch)");
        var rowReps = Row();
        rowReps.Controls.Add(new Label { Text = "Repeticiones (≥10):", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        rowReps.Controls.Add(_numReps);
        var rowRun = Row();
        rowRun.Controls.AddRange(new Control[] { _btnMeasure, _btnSweep });
        var rowLog = Row();
        var btnExport = new Button { Text = "Exportar CSV", AutoSize = true };
        btnExport.Click += (_, _) => ExportCsv();
        var btnClearLog = new Button { Text = "Limpiar registro", AutoSize = true };
        btnClearLog.Click += (_, _) => { _txtLog.Clear(); _csvRows.Clear(); };
        rowLog.Controls.AddRange(new Control[] { btnExport, btnClearLog });
        AddToGroup(gbMeasure, rowReps, rowRun, _txtLog, rowLog);

        side.Controls.AddRange(new Control[] { gbPoints, gbView, gbMeasure });
        Controls.Add(_canvas);
        Controls.Add(side);
    }

    private static GroupBox Group(string title) => new()
    {
        Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Width = 355, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 8)
    };

    private static FlowLayoutPanel Row() => new()
    {
        AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0)
    };

    private static void AddToGroup(GroupBox gb, params Control[] controls)
    {
        var inner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        inner.Controls.AddRange(controls);
        gb.Controls.Add(inner);
    }

    private void WireEvents()
    {
        _canvas.Paint += Canvas_Paint;
        _canvas.MouseDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += (_, _) => _dragIndex = -1;

        _numSamples.ValueChanged += (_, _) => Recompute();
        _trackT.ValueChanged += (_, _) => { UpdateInfo(); _canvas.Invalidate(); };
        foreach (var chk in new[] { _chkBernstein, _chkCasteljau, _chkPolygon, _chkConstruction, _chkSamples })
            chk.CheckedChanged += (_, _) => _canvas.Invalidate();

        _grid.CellValueChanged += Grid_CellValueChanged;
        _grid.UserDeletingRow += (_, e) =>
        {
            int idx = e.Row!.Index;
            if (idx >= 0 && idx < _points.Count) _points.RemoveAt(idx);
            BeginInvoke(PointsChanged);
        };

        _btnMeasure.Click += async (_, _) => await MeasureCurrentAsync();
        _btnSweep.Click += async (_, _) => await RunSweepAsync();
    }

    // ============================================================ Modelo
    private double CurrentT => _trackT.Value / 1000.0;

    private void LoadExampleCubic()
    {
        // Cúbica usada en la validación manual del informe: P0(0,0) P1(1,2) P2(3,2) P3(4,0),
        // escalada ×150 y desplazada para verse en pantalla (la escala no altera t).
        _points.Clear();
        foreach (var (x, y) in new[] { (0.0, 0.0), (1.0, 2.0), (3.0, 2.0), (4.0, 0.0) })
            _points.Add(new PointD(150 + 150 * x, 550 - 150 * y));
        PointsChanged();
    }

    private void GenerateRandom(int degree)
    {
        var rnd = new Random();
        int w = Math.Max(200, _canvas.ClientSize.Width - 80);
        int h = Math.Max(200, _canvas.ClientSize.Height - 80);
        _points.Clear();
        for (int i = 0; i <= degree; i++)
        {
            double x = 40 + (double)i / Math.Max(1, degree) * w;   // x creciente: curva legible
            double y = 40 + rnd.NextDouble() * h;
            _points.Add(new PointD(Math.Round(x), Math.Round(y)));
        }
        PointsChanged();
    }

    private void DeleteSelectedRow()
    {
        if (_grid.SelectedRows.Count == 0) return;
        int idx = _grid.SelectedRows[0].Index;
        if (idx >= 0 && idx < _points.Count)
        {
            _points.RemoveAt(idx);
            PointsChanged();
        }
    }

    private void PointsChanged()
    {
        RefreshGrid();
        Recompute();
    }

    private void Recompute()
    {
        int m = (int)_numSamples.Value;
        if (_points.Count == 0)
        {
            _bernstein = _casteljau = Array.Empty<PointD>();
        }
        else
        {
            _bernstein = Bernstein.EvaluateCurve(_points, m);
            _casteljau = DeCasteljau.EvaluateCurve(_points, m);
        }
        UpdateInfo();
        _canvas.Invalidate();
    }

    private void UpdateInfo()
    {
        double t = CurrentT;
        _lblT.Text = $"Parámetro t para la construcción: {t:0.000}";
        if (_points.Count == 0)
        {
            _lblInfo.Text = "Sin puntos de control.\nHaga clic en el lienzo para agregar.";
            return;
        }

        var pb = Bernstein.Evaluate(_points, t);
        var pc = DeCasteljau.Evaluate(_points, t);
        var diff = Metrics.Compare(_bernstein, _casteljau);
        var sb = new StringBuilder();
        sb.AppendLine($"Puntos de control: {_points.Count}   Grado n = {_points.Count - 1}");
        sb.AppendLine($"B(t) Bernstein   : ({pb.X:0.0000}; {pb.Y:0.0000})");
        sb.AppendLine($"B(t) De Casteljau: ({pc.X:0.0000}; {pc.Y:0.0000})");
        sb.AppendLine($"|ΔB(t)|          : {pb.DistanceTo(pc):0.###E+0}");
        sb.AppendLine($"Diferencia entre métodos ({_bernstein.Length} muestras):");
        sb.AppendLine($"  máx = {diff.Max:0.###E+0}  media = {diff.Mean:0.###E+0}");
        if (diff.InvalidPoints > 0) sb.AppendLine($"  ¡{diff.InvalidPoints} puntos no finitos (NaN/∞)!");
        _lblInfo.Text = sb.ToString();
    }

    private void RefreshGrid()
    {
        _suppressGrid = true;
        _grid.Rows.Clear();
        for (int i = 0; i < _points.Count; i++)
            _grid.Rows.Add($"P{i}", _points[i].X.ToString("0.###"), _points[i].Y.ToString("0.###"));
        _suppressGrid = false;
    }

    private void UpdateGridRow(int i)
    {
        if (i < 0 || i >= _grid.Rows.Count) return;
        _suppressGrid = true;
        _grid.Rows[i].Cells[1].Value = _points[i].X.ToString("0.###");
        _grid.Rows[i].Cells[2].Value = _points[i].Y.ToString("0.###");
        _suppressGrid = false;
    }

    private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressGrid || e.RowIndex < 0 || e.RowIndex >= _points.Count || e.ColumnIndex < 1) return;
        string? text = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
        {
            var p = _points[e.RowIndex];
            _points[e.RowIndex] = e.ColumnIndex == 1 ? p with { X = v } : p with { Y = v };
            Recompute();
        }
        else
        {
            UpdateGridRow(e.RowIndex); // valor inválido: se restaura
        }
    }

    // ============================================================ Ratón
    private int HitTest(Point location)
    {
        for (int i = _points.Count - 1; i >= 0; i--)
        {
            double dx = _points[i].X - location.X, dy = _points[i].Y - location.Y;
            if (dx * dx + dy * dy <= HitRadius * HitRadius) return i;
        }
        return -1;
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        int hit = HitTest(e.Location);
        if (e.Button == MouseButtons.Left)
        {
            if (hit >= 0) _dragIndex = hit;
            else
            {
                _points.Add(new PointD(e.X, e.Y));
                _dragIndex = _points.Count - 1;
                PointsChanged();
            }
        }
        else if (e.Button == MouseButtons.Right && hit >= 0)
        {
            _points.RemoveAt(hit);
            PointsChanged();
        }
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        _canvas.Cursor = HitTest(e.Location) >= 0 || _dragIndex >= 0 ? Cursors.Hand : Cursors.Cross;
        if (_dragIndex < 0 || e.Button != MouseButtons.Left) return;
        _points[_dragIndex] = new PointD(e.X, e.Y);
        UpdateGridRow(_dragIndex);
        Recompute();
    }

    // ============================================================ Dibujo
    // Nota: la curva se dibuja como una polilínea que une los puntos ya calculados por
    // nuestros algoritmos (DrawLines). No se usa Graphics.DrawBezier ni ninguna función
    // gráfica que genere la curva directamente.
    private static PointF[] ToPointF(IReadOnlyList<PointD> pts)
    {
        var arr = new PointF[pts.Count];
        for (int i = 0; i < arr.Length; i++)
        {
            double x = pts[i].X, y = pts[i].Y;
            arr[i] = double.IsFinite(x) && double.IsFinite(y) ? new PointF((float)x, (float)y) : PointF.Empty;
        }
        return arr;
    }

    private static readonly Color[] LevelColors =
    {
        Color.FromArgb(0, 150, 136), Color.FromArgb(255, 152, 0), Color.FromArgb(156, 39, 176),
        Color.FromArgb(76, 175, 80), Color.FromArgb(233, 30, 99), Color.FromArgb(63, 81, 181),
        Color.FromArgb(121, 85, 72), Color.FromArgb(0, 188, 212)
    };

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var smallFont = new Font("Segoe UI", 8f);
        using var boldFont = new Font("Segoe UI", 9f, FontStyle.Bold);

        if (_chkPolygon.Checked && _points.Count >= 2)
        {
            using var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash };
            g.DrawLines(pen, ToPointF(_points));
        }

        if (_chkBernstein.Checked && _bernstein.Length >= 2)
        {
            using var pen = new Pen(Color.FromArgb(110, 30, 100, 230), 7f) { LineJoin = LineJoin.Round };
            g.DrawLines(pen, ToPointF(_bernstein));
        }

        if (_chkCasteljau.Checked && _casteljau.Length >= 2)
        {
            using var pen = new Pen(Color.FromArgb(220, 30, 30), 1.6f);
            g.DrawLines(pen, ToPointF(_casteljau));
        }

        if (_chkSamples.Checked && _casteljau.Length <= 5000)
        {
            using var brush = new SolidBrush(Color.FromArgb(200, 20, 20, 20));
            foreach (var p in ToPointF(_casteljau))
                g.FillEllipse(brush, p.X - 1.8f, p.Y - 1.8f, 3.6f, 3.6f);
        }

        if (_chkConstruction.Checked && _points.Count >= 2)
        {
            var levels = DeCasteljau.Levels(_points, CurrentT);
            for (int r = 1; r < levels.Count - 1; r++)
            {
                var color = LevelColors[(r - 1) % LevelColors.Length];
                using var pen = new Pen(color, 1.3f);
                using var brush = new SolidBrush(color);
                var pts = ToPointF(levels[r]);
                if (pts.Length >= 2) g.DrawLines(pen, pts);
                foreach (var p in pts) g.FillEllipse(brush, p.X - 3.5f, p.Y - 3.5f, 7, 7);
            }
            var final = levels[^1][0];
            g.FillEllipse(Brushes.Black, (float)final.X - 6, (float)final.Y - 6, 12, 12);
            g.DrawString($"B({CurrentT:0.00})", boldFont, Brushes.Black, (float)final.X + 8, (float)final.Y + 4);
        }

        // Puntos de control
        using (var fill = new SolidBrush(Color.FromArgb(255, 193, 7)))
        using (var border = new Pen(Color.FromArgb(90, 70, 0), 1.5f))
        {
            for (int i = 0; i < _points.Count; i++)
            {
                float x = (float)_points[i].X, y = (float)_points[i].Y;
                g.FillEllipse(fill, x - 6, y - 6, 12, 12);
                g.DrawEllipse(border, x - 6, y - 6, 12, 12);
                g.DrawString($"P{i}", smallFont, Brushes.Black, x + 7, y - 16);
            }
        }

        // Leyenda
        g.FillRectangle(new SolidBrush(Color.FromArgb(230, 255, 255, 255)), 8, 8, 300, 62);
        using (var pb = new Pen(Color.FromArgb(110, 30, 100, 230), 7f)) g.DrawLine(pb, 16, 22, 56, 22);
        using (var pc = new Pen(Color.FromArgb(220, 30, 30), 1.6f)) g.DrawLine(pc, 16, 42, 56, 42);
        g.DrawString("Bernstein (evaluación directa)", Font, Brushes.Black, 64, 14);
        g.DrawString("De Casteljau (interpolación)", Font, Brushes.Black, 64, 34);
        g.DrawString("Si ambas coinciden, la línea roja queda centrada en la azul.", smallFont, Brushes.DimGray, 16, 52);
    }

    // ============================================================ Medición
    private void SetBusy(bool busy)
    {
        _busy = busy;
        _btnMeasure.Enabled = _btnSweep.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void Log(string line) => _txtLog.AppendText(line + Environment.NewLine);

    private static string Csv(FormattableString s) => FormattableString.Invariant(s);

    private record MeasureResult(int Degree, int Samples, int Reps, TimingStats Bernstein, TimingStats Casteljau, DifferenceStats Diff);

    /// <summary>Solo se cronometra el cálculo de los puntos; el dibujo queda fuera de la medición.</summary>
    private static MeasureResult MeasureCore(IReadOnlyList<PointD> points, int samples, int reps)
    {
        var pts = points.ToArray();
        var destB = new PointD[samples];
        var destC = new PointD[samples];
        var tb = Metrics.Measure(() => Bernstein.EvaluateCurve(pts, destB), reps);
        var tc = Metrics.Measure(() => DeCasteljau.EvaluateCurve(pts, destC), reps);
        return new MeasureResult(pts.Length - 1, samples, reps, tb, tc, Metrics.Compare(destB, destC));
    }

    private void Report(MeasureResult r)
    {
        Log($"Grado {r.Degree,3} | m={r.Samples,7} | reps={r.Reps}");
        Log($"  Bernstein   : media {r.Bernstein.MeanMs,9:0.0000} ms  σ {r.Bernstein.StdDevMs:0.0000}  [{r.Bernstein.MinMs:0.0000} – {r.Bernstein.MaxMs:0.0000}]");
        Log($"  De Casteljau: media {r.Casteljau.MeanMs,9:0.0000} ms  σ {r.Casteljau.StdDevMs:0.0000}  [{r.Casteljau.MinMs:0.0000} – {r.Casteljau.MaxMs:0.0000}]");
        double ratio = r.Casteljau.MeanMs / r.Bernstein.MeanMs;
        Log($"  DC/Bernstein = {ratio:0.00}×   Δmáx = {r.Diff.Max:0.##E+0}   Δmedia = {r.Diff.Mean:0.##E+0}");

        string date = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
        foreach (var (name, s) in new[] { ("Bernstein", r.Bernstein), ("DeCasteljau", r.Casteljau) })
            _csvRows.Add(Csv($"{date},{r.Degree},{r.Degree + 1},{r.Samples},{r.Reps},{name},{s.MeanMs:R},{s.StdDevMs:R},{s.MinMs:R},{s.MaxMs:R},{s.MedianMs:R},{r.Diff.Max:R},{r.Diff.Mean:R}"));
    }

    private async Task MeasureCurrentAsync()
    {
        if (_busy || _points.Count < 2) { if (_points.Count < 2) Log("Se necesitan al menos 2 puntos de control."); return; }
        SetBusy(true);
        try
        {
            var pts = _points.ToArray();
            int m = (int)_numSamples.Value, reps = (int)_numReps.Value;
            var r = await Task.Run(() => MeasureCore(pts, m, reps));
            Report(r);
        }
        finally { SetBusy(false); }
    }

    private async Task RunSweepAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            int reps = (int)_numReps.Value;
            int[] degrees = { 3, 5, 10, 20, 30, 50 };
            int[] samples = { 100, 1000, 10000 };
            Log($"=== Experimento: grados {{{string.Join(",", degrees)}}} × muestras {{{string.Join(",", samples)}}}, {reps} repeticiones ===");
            foreach (int n in degrees)
            {
                var pts = Metrics.RandomControlPoints(n + 1, seed: 1000 + n);
                foreach (int m in samples)
                {
                    var r = await Task.Run(() => MeasureCore(pts, m, reps));
                    Report(r);
                }
            }
            Log("=== Fin del experimento ===");
        }
        finally { SetBusy(false); }
    }

    private void ExportCsv()
    {
        if (_csvRows.Count == 0) { MessageBox.Show(this, "No hay mediciones registradas.", Text); return; }
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "mediciones_bezier.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var lines = new List<string> { "fecha,grado,puntos_control,muestras,repeticiones,metodo,media_ms,desv_ms,min_ms,max_ms,mediana_ms,dif_max,dif_media" };
        lines.AddRange(_csvRows);
        File.WriteAllLines(dlg.FileName, lines, Encoding.UTF8);
        Log($"Exportado: {dlg.FileName}");
    }
}
