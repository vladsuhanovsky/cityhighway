using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Manually triggered synthetic CPU benchmark. No gameplay or CI pass/fail thresholds.
/// </summary>
public static class City3DMeshAppendBenchmark
{
    const int SourceQuads = 12;
    const int AppendsPerRun = 250;
    const int WarmupRuns = 2;
    const int MeasuredRuns = 7;

    [MenuItem("City Highways/3D Objects/Benchmark MeshBuilder Append")]
    static void Run()
    {
        var source = new MeshBuilder(SourceQuads);
        var uv = new Rect(0f, 0f, 1f, 1f);
        for (int i = 0; i < SourceQuads; i++)
        {
            float x = i * 2f;
            source.Quad(new Vector3(x, 0f, 0f),
                new Vector3(x, 0f, 1f),
                new Vector3(x + 1f, 0f, 1f),
                new Vector3(x + 1f, 0f, 0f), uv);
        }

        var identity = Matrix4x4.identity;
        var translation = Matrix4x4.Translate(new Vector3(3f, -1f, 2f));
        var rotationScale = Matrix4x4.TRS(new Vector3(3f, -1f, 2f),
            Quaternion.Euler(10f, 25f, 5f), new Vector3(1.5f, 0.8f, 1.2f));

        var labels = new[] { "Identity", "Translation", "Rotation + scale" };
        var runs = new double[3][];
        string identityReport = Format(labels[0], source, identity, out runs[0]);
        string translationReport = Format(labels[1], source, translation, out runs[1]);
        string rotationReport = Format(labels[2], source, rotationScale, out runs[2]);

        UnityEngine.Debug.Log(
            "MeshBuilder.Append synthetic CPU benchmark — " +
            "same source mesh, " + AppendsPerRun + " appends/run, " +
            WarmupRuns + " warmups + " + MeasuredRuns + " measured runs.\n" +
            identityReport + "\n" +
            translationReport + "\n" +
            rotationReport + "\n" +
            "Editor timings include managed/runtime overhead; compare identical " +
            "Unity versions, hardware and configurations. This is not FPS or GPU time.");

        string destinationPath = EditorUtility.SaveFilePanel(
            "Save mesh append benchmark CSV (Cancel to skip)",
            "", "city3d-mesh-append-benchmark.csv", "csv");
        if (!string.IsNullOrEmpty(destinationPath))
        {
            var csv = City3DMeshAppendBenchmarkCsv.Export(DateTime.UtcNow,
                Application.unityVersion, SourceQuads, AppendsPerRun, labels, runs);
            File.WriteAllText(destinationPath, csv, new UTF8Encoding(false));
            UnityEngine.Debug.Log("MeshBuilder.Append benchmark CSV saved: " + destinationPath);
        }
    }

    static string Format(string label, MeshBuilder source, Matrix4x4 transform,
        out double[] samples)
    {
        var elapsed = new double[MeasuredRuns];
        for (int run = -WarmupRuns; run < MeasuredRuns; run++)
        {
            // Deliberately includes destination construction and capacity growth,
            // matching the repeated small-append use case.
            var destination = new MeshBuilder();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < AppendsPerRun; i++)
                destination.Append(source, transform);
            timer.Stop();

            int expectedVertices = checked(source.VertexCount * AppendsPerRun);
            if (destination.VertexCount != expectedVertices)
                throw new InvalidOperationException("Benchmark vertex count mismatch.");
            if (run >= 0) elapsed[run] = timer.Elapsed.TotalMilliseconds;
        }

        samples = (double[])elapsed.Clone();
        Array.Sort(elapsed);
        double median = elapsed[MeasuredRuns / 2];
        return label + ": median " + median.ToString("F3") +
            " ms/run (" + (median * 1000d / AppendsPerRun).ToString("F2") +
            " microseconds/append), range " + elapsed[0].ToString("F3") +
            "–" + elapsed[MeasuredRuns - 1].ToString("F3") + " ms.";
    }
}
