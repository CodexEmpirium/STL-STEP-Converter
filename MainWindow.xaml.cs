using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace StlStepConverter;

public partial class MainWindow : Window
{
    private string? _stlPath;
    private string? _stepPath;

    public MainWindow()
    {
        InitializeComponent();
        FreeCadPathBox.Text = FreeCadConverter.FindFreeCadCommand() ?? "";
        StatusText.Text = string.IsNullOrWhiteSpace(FreeCadPathBox.Text)
            ? "Select FreeCADCmd.exe before converting."
            : "Ready";
    }

    private void BrowseFreeCad_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select FreeCADCmd.exe",
            Filter = "FreeCAD command (FreeCADCmd.exe)|FreeCADCmd.exe|Executables (*.exe)|*.exe|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            FreeCadPathBox.Text = dialog.FileName;
            StatusText.Text = "FreeCAD command selected.";
        }
    }

    private void OpenStl_Click(object sender, RoutedEventArgs e)
    {
        var path = PickInputFile("Open STL file", "STL files (*.stl)|*.stl|All files (*.*)|*.*");
        if (path is not null)
        {
            SetStlPath(path);
        }
    }

    private void OpenStep_Click(object sender, RoutedEventArgs e)
    {
        var path = PickInputFile("Open STEP file", "STEP files (*.step;*.stp)|*.step;*.stp|All files (*.*)|*.*");
        if (path is not null)
        {
            SetStepPath(path);
        }
    }

    private async void ConvertStlToStep_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFreeCad() || !ValidateInput(_stlPath, ".stl"))
        {
            return;
        }

        var outputPath = PickOutputFile("Save STEP file", "STEP files (*.step)|*.step|STP files (*.stp)|*.stp", _stlPath!, ".step");
        if (outputPath is null)
        {
            return;
        }

        await RunConversionAsync(() => FreeCadConverter.ConvertStlToStepAsync(FreeCadPathBox.Text, _stlPath!, outputPath), outputPath);
        SetStepPath(outputPath);
    }

    private async void ConvertStepToStl_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFreeCad() || !ValidateInput(_stepPath, ".step", ".stp"))
        {
            return;
        }

        var outputPath = PickOutputFile("Save STL file", "STL files (*.stl)|*.stl", _stepPath!, ".stl");
        if (outputPath is null)
        {
            return;
        }

        await RunConversionAsync(() => FreeCadConverter.ConvertStepToStlAsync(FreeCadPathBox.Text, _stepPath!, outputPath), outputPath);
        SetStlPath(outputPath);
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void StlDropZone_Drop(object sender, DragEventArgs e)
    {
        var path = GetDroppedFile(e, ".stl");
        if (path is not null)
        {
            SetStlPath(path);
        }
    }

    private void StepDropZone_Drop(object sender, DragEventArgs e)
    {
        var path = GetDroppedFile(e, ".step", ".stp");
        if (path is not null)
        {
            SetStepPath(path);
        }
    }

    private async Task RunConversionAsync(Func<Task<ConversionResult>> conversion, string outputPath)
    {
        SetBusy(true);
        StatusText.Text = "Converting with FreeCAD...";

        try
        {
            var result = await conversion();
            StatusText.Text = result.Success
                ? $"Created {outputPath}"
                : $"Conversion failed. {result.ErrorOutput}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Conversion failed. {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
        StlDropZone.IsEnabled = !busy;
        StepDropZone.IsEnabled = !busy;
    }

    private bool ValidateFreeCad()
    {
        if (File.Exists(FreeCadPathBox.Text))
        {
            return true;
        }

        StatusText.Text = "Select a valid FreeCADCmd.exe path before converting.";
        return false;
    }

    private bool ValidateInput(string? path, params string[] extensions)
    {
        if (path is null || !File.Exists(path))
        {
            StatusText.Text = "Select or drop a source file first.";
            return false;
        }

        if (extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        StatusText.Text = $"Expected a {string.Join(" or ", extensions)} file.";
        return false;
    }

    private static string? PickInputFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PickOutputFile(string title, string filter, string sourcePath, string targetExtension)
    {
        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            InitialDirectory = sourceDirectory,
            FileName = sourceName + targetExtension,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? GetDroppedFile(DragEventArgs e, params string[] allowedExtensions)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.FirstOrDefault(file =>
            File.Exists(file) &&
            allowedExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
    }

    private void SetStlPath(string path)
    {
        _stlPath = path;
        StlFileBox.Text = path;
        StlDirectoryBox.Text = Path.GetDirectoryName(path) ?? "";
        StatusText.Text = "STL file selected.";
    }

    private void SetStepPath(string path)
    {
        _stepPath = path;
        StepFileBox.Text = path;
        StepDirectoryBox.Text = Path.GetDirectoryName(path) ?? "";
        StatusText.Text = "STEP file selected.";
    }
}

internal static class FreeCadConverter
{
    public static async Task<ConversionResult> ConvertStlToStepAsync(string freeCadCommand, string inputPath, string outputPath)
    {
        var script = $$"""
import FreeCAD
import Mesh
import Part
import Import

input_path = {{Py(inputPath)}}
output_path = {{Py(outputPath)}}
tolerance = 0.1

doc = FreeCAD.newDocument("StlToStep")
mesh = Mesh.Mesh(input_path)

shape = Part.Shape()
shape.makeShapeFromMesh(mesh.Topology, tolerance)

try:
    shape = Part.makeSolid(shape)
except Exception:
    pass

try:
    shape = shape.removeSplitter()
except Exception:
    pass

feature = doc.addObject("Part::Feature", "Converted")
feature.Shape = shape
doc.recompute()
Import.export([feature], output_path)
FreeCAD.closeDocument(doc.Name)
""";

        return await RunFreeCadScriptAsync(freeCadCommand, script);
    }

    public static async Task<ConversionResult> ConvertStepToStlAsync(string freeCadCommand, string inputPath, string outputPath)
    {
        var script = $$"""
import FreeCAD
import Part
import Mesh
import MeshPart

input_path = {{Py(inputPath)}}
output_path = {{Py(outputPath)}}

shape = Part.Shape()
shape.read(input_path)
mesh = MeshPart.meshFromShape(
    Shape=shape,
    LinearDeflection=0.1,
    AngularDeflection=0.523599,
    Relative=False
)
mesh.write(output_path)
""";

        return await RunFreeCadScriptAsync(freeCadCommand, script);
    }

    public static string? FindFreeCadCommand()
    {
        var candidates = new List<string>();

        var envPath = Environment.GetEnvironmentVariable("FREECAD_CMD");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            candidates.Add(envPath);
        }

        candidates.AddRange(new[]
        {
            @"C:\Program Files\FreeCAD 1.0\bin\FreeCADCmd.exe",
            @"C:\Program Files\FreeCAD 0.21\bin\FreeCADCmd.exe",
            @"C:\Program Files\FreeCAD 0.20\bin\FreeCADCmd.exe",
            @"C:\Program Files\FreeCAD\bin\FreeCADCmd.exe"
        });

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        candidates.AddRange(pathValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path.Trim(), "FreeCADCmd.exe")));

        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<ConversionResult> RunFreeCadScriptAsync(string freeCadCommand, string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"stl-step-converter-{Guid.NewGuid():N}.py");
        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = freeCadCommand,
                Arguments = Quote(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start FreeCADCmd.exe.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new ConversionResult(process.ExitCode == 0, MergeOutput(stdout, stderr));
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // Temporary script cleanup failure should not hide conversion results.
            }
        }
    }

    private static string Py(string value) => JsonSerializer.Serialize(value);

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string MergeOutput(string stdout, string stderr)
    {
        var output = (stdout + Environment.NewLine + stderr).Trim();
        return string.IsNullOrWhiteSpace(output) ? "FreeCAD did not report details." : output;
    }
}

internal sealed record ConversionResult(bool Success, string ErrorOutput);
