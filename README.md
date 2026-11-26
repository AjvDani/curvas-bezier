# Curvas de Bézier: Bernstein frente a De Casteljau

Comparación del rendimiento, la estabilidad numérica y la complejidad de dos métodos de evaluación de curvas de Bézier: la evaluación directa con la base de Bernstein y el algoritmo de De Casteljau.

| Entregable | Ubicación |
|---|---|
| Código fuente (algoritmos) | `src/BezierCore`: no usa ninguna API gráfica |

> Proyecto en construcción: la aplicación gráfica (Parte 2) y el banco de pruebas con los resultados y el informe (Parte 3) se agregan en las siguientes ramas.

Requisito: .NET SDK 10.

Compilar la solución:

```powershell
dotnet build BezierComparacion.slnx -c Release
```
