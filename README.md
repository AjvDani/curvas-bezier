# Curvas de Bézier: Bernstein frente a De Casteljau

Comparación del rendimiento, la estabilidad numérica y la complejidad de dos métodos de evaluación de curvas de Bézier: la evaluación directa con la base de Bernstein y el algoritmo de De Casteljau.

| Entregable | Ubicación |
|---|---|
| Código fuente (algoritmos) | `src/BezierCore`: no usa ninguna API gráfica |
| Aplicación funcional (WinForms) | `src/BezierApp` |
| Banco de pruebas (Stopwatch, ≥10 repeticiones) | `src/BezierBenchmark` |
| Resultados (CSV, tablas, gráficos SVG) | `resultados/` |
| Informe técnico (Word, con la carátula ESPE) | `docs/Informe_Tecnico_Bezier.docx` |
| Informe técnico (Markdown) | `docs/Informe_Tecnico.md` |

Requisito: .NET SDK 10.

Compilar la solución:

```powershell
dotnet build BezierComparacion.slnx -c Release
```

Abrir la aplicación gráfica:

```powershell
dotnet run -c Release --project src/BezierApp
```

Ejecutar los experimentos (carpeta de salida y número de repeticiones):

```powershell
dotnet run -c Release --project src/BezierBenchmark -- resultados 30
```

## Uso de la aplicación

- **Lienzo:** clic izquierdo agrega un punto de control, arrastrar lo mueve y clic derecho lo elimina.
- **Tabla:** permite editar las coordenadas X e Y de cada punto.
- **Muestras (m):** número de puntos evaluados en la curva.
- **Deslizador de t:** muestra las interpolaciones de De Casteljau para ese valor de t.
- **Medir curva actual:** mide la curva actual con N repeticiones (N ≥ 10). Solo se cronometra el cálculo; el dibujo queda fuera de la medición.
- **Experimento grado × muestras:** ejecuta un barrido completo. Los resultados se pueden exportar a CSV.
