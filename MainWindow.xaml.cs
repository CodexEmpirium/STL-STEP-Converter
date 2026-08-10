using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace StlStepConverter;

public partial class MainWindow : Window
{
    private string? _stlPath;
    private string? _stepPath;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Stopwatch _conversionStopwatch = new();
    private string _progressMessage = "Conversion progress";
    private int _progressPercent;

    public MainWindow()
    {
        InitializeComponent();
        _elapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _elapsedTimer.Tick += ElapsedTimer_Tick;
        FreeCadPathBox.Text = FreeCadConverter.FindFreeCadCommand() ?? "";
        StatusText.Text = string.IsNullOrWhiteSpace(FreeCadPathBox.Text)
            ? "FreeCADCmd.exe was not found automatically. Browse to it once, then open or drop a file."
            : "Open or drop an STL or STEP file to convert automatically.";
        UpdateProgress("Conversion progress", 0);
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

    private async void OpenStl_Click(object sender, RoutedEventArgs e)
    {
        var path = PickInputFile("Open STL file", "STL files (*.stl)|*.stl|All files (*.*)|*.*");
        if (path is not null)
        {
            ClearStepPath();
            SetStlPath(path);
            await ConvertStlToStepAsync(path);
        }
    }

    private async void OpenStep_Click(object sender, RoutedEventArgs e)
    {
        var path = PickInputFile("Open STEP file", "STEP files (*.step;*.stp)|*.step;*.stp|All files (*.*)|*.*");
        if (path is not null)
        {
            ClearStlPath();
            SetStepPath(path);
            await ConvertStepToStlAsync(path);
        }
    }

    private async Task ConvertStlToStepAsync(string inputPath)
    {
        if (!ValidateFreeCad() || !ValidateInput(inputPath, ".stl"))
        {
            return;
        }

        var outputPath = Path.ChangeExtension(inputPath, ".step");
        if (await RunConversionAsync(progress => FreeCadConverter.ConvertStlToStepAsync(FreeCadPathBox.Text, inputPath, outputPath, progress), outputPath, "Converting STL -> STEP"))
        {
            SetStepPath(outputPath);
            StatusText.Text = $"Created {outputPath}";
        }
    }

    private async Task ConvertStepToStlAsync(string inputPath)
    {
        if (!ValidateFreeCad() || !ValidateInput(inputPath, ".step", ".stp"))
        {
            return;
        }

        var outputPath = Path.ChangeExtension(inputPath, ".stl");
        if (await RunConversionAsync(progress => FreeCadConverter.ConvertStepToStlAsync(FreeCadPathBox.Text, inputPath, outputPath, progress), outputPath, "Converting STL <- STEP"))
        {
            SetStlPath(outputPath);
            StatusText.Text = $"Created {outputPath}";
        }
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void StlDropZone_Drop(object sender, DragEventArgs e)
    {
        var path = GetDroppedFile(e, ".stl");
        if (path is not null)
        {
            ClearStepPath();
            SetStlPath(path);
            await ConvertStlToStepAsync(path);
        }
    }

    private async void StepDropZone_Drop(object sender, DragEventArgs e)
    {
        var path = GetDroppedFile(e, ".step", ".stp");
        if (path is not null)
        {
            ClearStlPath();
            SetStepPath(path);
            await ConvertStepToStlAsync(path);
        }
    }

    private void ViewStl_Click(object sender, RoutedEventArgs e)
    {
        OpenInFreeCad(_stlPath);
    }

    private void ViewStep_Click(object sender, RoutedEventArgs e)
    {
        OpenInFreeCad(_stepPath);
    }

    private async Task<bool> RunConversionAsync(Func<Action<ConversionProgress>, Task<ConversionResult>> conversion, string outputPath, string progressMessage)
    {
        SetBusy(true);
        BeginProgress(progressMessage);
        StatusText.Text = "Converting with FreeCAD...";

        try
        {
            var result = await conversion(HandleConversionProgress);
            FinishProgress(result.Success);
            StatusText.Text = result.Success
                ? $"Created {outputPath}"
                : $"Conversion failed. {result.ErrorOutput}";
            return result.Success;
        }
        catch (Exception ex)
        {
            FinishProgress(false);
            StatusText.Text = $"Conversion failed. {ex.Message}";
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ElapsedTimer_Tick(object? sender, EventArgs e)
    {
        UpdateElapsedTime();
    }

    private void BeginProgress(string message)
    {
        _progressMessage = message;
        ConversionLogBox.Clear();
        _conversionStopwatch.Restart();
        _elapsedTimer.Start();
        UpdateProgress(message, 0);
        UpdateElapsedTime();
        AppendLog($"{DateTime.Now:HH:mm:ss} Started {message}");
    }

    private void FinishProgress(bool success)
    {
        _elapsedTimer.Stop();
        _conversionStopwatch.Stop();
        UpdateProgress(_progressMessage, success ? 100 : _progressPercent);
        UpdateElapsedTime();
        AppendLog($"{DateTime.Now:HH:mm:ss} {(success ? "Completed" : "Stopped")} {_progressMessage}");
    }

    private void UpdateProgress(string message, int percent)
    {
        _progressPercent = Math.Clamp(percent, 0, 100);
        ConversionProgressBar.Value = _progressPercent;
        ProgressText.Text = $"{message} done {_progressPercent} %";
    }

    private void HandleConversionProgress(ConversionProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            if (progress.Percent is not null)
            {
                UpdateProgress(_progressMessage, progress.Percent.Value);
            }

            if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} {progress.Message}");
            }
        });
    }

    private void UpdateElapsedTime()
    {
        ElapsedTimeText.Text = $"Elapsed time {_conversionStopwatch.Elapsed:hh\\:mm\\:ss}";
    }

    private void AppendLog(string message)
    {
        ConversionLogBox.AppendText(message + Environment.NewLine);
        ConversionLogBox.ScrollToEnd();
    }

    private void SetBusy(bool busy)
    {
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
        StlDropZone.IsEnabled = !busy;
        StepDropZone.IsEnabled = !busy;
    }

    private bool ValidateFreeCad()
    {
        if (!File.Exists(FreeCadPathBox.Text))
        {
            FreeCadPathBox.Text = FreeCadConverter.FindFreeCadCommand() ?? "";
        }

        if (File.Exists(FreeCadPathBox.Text))
        {
            return true;
        }

        StatusText.Text = "FreeCADCmd.exe was not found. Use Browse to select it, then open or drop the file again.";
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
        ViewStlButton.IsEnabled = File.Exists(path);
        StatusText.Text = "STL file selected.";
    }

    private void SetStepPath(string path)
    {
        _stepPath = path;
        StepFileBox.Text = path;
        StepDirectoryBox.Text = Path.GetDirectoryName(path) ?? "";
        ViewStepButton.IsEnabled = File.Exists(path);
        StatusText.Text = "STEP file selected.";
    }

    private void ClearStlPath()
    {
        _stlPath = null;
        StlFileBox.Text = "";
        StlDirectoryBox.Text = "";
        ViewStlButton.IsEnabled = false;
    }

    private void ClearStepPath()
    {
        _stepPath = null;
        StepFileBox.Text = "";
        StepDirectoryBox.Text = "";
        ViewStepButton.IsEnabled = false;
    }

    private void OpenInFreeCad(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            StatusText.Text = "There is no file to view yet.";
            return;
        }

        var freeCadPath = FreeCadConverter.FindFreeCadGui(FreeCadPathBox.Text);
        if (freeCadPath is null)
        {
            StatusText.Text = "FreeCAD.exe was not found. Check your FreeCAD installation.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = freeCadPath,
                Arguments = FreeCadConverter.QuoteArgument(path),
                UseShellExecute = false
            });
            StatusText.Text = $"Opened {path} in FreeCAD.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not open FreeCAD. {ex.Message}";
        }
    }
}

internal static class FreeCadConverter
{
    public static async Task<ConversionResult> ConvertStlToStepAsync(string freeCadCommand, string inputPath, string outputPath, Action<ConversionProgress> progress)
    {
        var script = $$"""
import FreeCAD
import Mesh
import Part
import Import
import sys

def progress(percent, message):
    print("STLSTEP_PROGRESS|{}|{}".format(int(percent), message), flush=True)

def log(message):
    print("STLSTEP_LOG|{}".format(message), flush=True)

input_path = {{Py(inputPath)}}
output_path = {{Py(outputPath)}}
tolerance = 0.1

progress(0, "Loading STL mesh")
doc = FreeCAD.newDocument("StlToStep")
mesh = Mesh.Mesh(input_path)
facet_count = mesh.CountFacets
component_count = max(1, mesh.countComponents())
log("Loaded STL mesh with {} facets and {} component(s)".format(facet_count, component_count))
progress(8, "Splitting STL mesh into CAD artifacts")

try:
    components = mesh.getSeparateComponents()
except Exception as exc:
    log("Could not split mesh components; converting as one artifact: {}".format(exc))
    components = [mesh]

if not components:
    components = [mesh]

features = []
total = len(components)
log("Converting {} mesh artifact(s) to STEP geometry".format(total))

for index, component in enumerate(components, start=1):
    artifact_facets = component.CountFacets
    progress(10 + ((index - 1) * 70 / total), "Converting STL artifact {}/{} ({} facets)".format(index, total, artifact_facets))
    shape = Part.Shape()
    shape.makeShapeFromMesh(component.Topology, tolerance)

    try:
        shape = Part.makeSolid(shape)
    except Exception as exc:
        log("Artifact {}/{} remained a shell: {}".format(index, total, exc))

    try:
        shape = shape.removeSplitter()
    except Exception:
        pass

    feature = doc.addObject("Part::Feature", "Converted_{}".format(index))
    feature.Shape = shape
    features.append(feature)
    progress(10 + (index * 70 / total), "Converted STL artifact {}/{}".format(index, total))

progress(84, "Recomputing FreeCAD document")
doc.recompute()
progress(92, "Writing STEP file")
Import.export(features, output_path)
progress(100, "STEP file written")
FreeCAD.closeDocument(doc.Name)
""";

        return await RunFreeCadScriptAsync(freeCadCommand, script, outputPath, progress);
    }

    public static async Task<ConversionResult> ConvertStepToStlAsync(string freeCadCommand, string inputPath, string outputPath, Action<ConversionProgress> progress)
    {
        var script = $$"""
import FreeCAD
import Part
import Mesh
import MeshPart
import sys

def progress(percent, message):
    print("STLSTEP_PROGRESS|{}|{}".format(int(percent), message), flush=True)

def log(message):
    print("STLSTEP_LOG|{}".format(message), flush=True)

input_path = {{Py(inputPath)}}
output_path = {{Py(outputPath)}}

progress(0, "Loading STEP shape")
shape = Part.Shape()
shape.read(input_path)
progress(8, "Counting STEP CAD artifacts")

artifacts = list(shape.Solids)
artifact_kind = "solid"
if not artifacts:
    artifacts = list(shape.Shells)
    artifact_kind = "shell"
if not artifacts:
    artifacts = list(shape.Faces)
    artifact_kind = "face"
if not artifacts:
    artifacts = [shape]
    artifact_kind = "shape"

total = len(artifacts)
log("Loaded STEP shape with {} {} artifact(s)".format(total, artifact_kind))
combined = Mesh.Mesh()

for index, artifact in enumerate(artifacts, start=1):
    progress(10 + ((index - 1) * 78 / total), "Meshing STEP {} {}/{}".format(artifact_kind, index, total))
    artifact_mesh = MeshPart.meshFromShape(
        Shape=artifact,
        LinearDeflection=0.1,
        AngularDeflection=0.523599,
        Relative=False
    )
    combined.addMesh(artifact_mesh)
    log("Meshed STEP {} {}/{} into {} facets".format(artifact_kind, index, total, artifact_mesh.CountFacets))
    progress(10 + (index * 78 / total), "Meshed STEP {} {}/{}".format(artifact_kind, index, total))

progress(94, "Writing STL file")
combined.write(output_path)
progress(100, "STL file written")
""";

        return await RunFreeCadScriptAsync(freeCadCommand, script, outputPath, progress);
    }

    public static string? FindFreeCadCommand()
    {
        var candidates = new List<string>();

        var envPath = Environment.GetEnvironmentVariable("FREECAD_CMD");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            candidates.Add(envPath);
        }

        candidates.AddRange(FindFreeCadCommandsInProgramFiles());

        candidates.AddRange(new[]
        {
            @"C:\Program Files\FreeCAD 1.1\bin\FreeCADCmd.exe",
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

    public static string? FindFreeCadGui(string? freeCadCommand)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(freeCadCommand))
        {
            var commandDirectory = Path.GetDirectoryName(freeCadCommand);
            if (!string.IsNullOrWhiteSpace(commandDirectory))
            {
                candidates.Add(Path.Combine(commandDirectory, "FreeCAD.exe"));
            }
        }

        candidates.AddRange(FindFreeCadCommandsInProgramFiles()
            .Select(command => Path.Combine(Path.GetDirectoryName(command) ?? "", "FreeCAD.exe")));

        candidates.AddRange(new[]
        {
            @"C:\Program Files\FreeCAD 1.1\bin\FreeCAD.exe",
            @"C:\Program Files\FreeCAD 1.0\bin\FreeCAD.exe",
            @"C:\Program Files\FreeCAD 0.21\bin\FreeCAD.exe",
            @"C:\Program Files\FreeCAD 0.20\bin\FreeCAD.exe",
            @"C:\Program Files\FreeCAD\bin\FreeCAD.exe"
        });

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        candidates.AddRange(pathValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path.Trim(), "FreeCAD.exe")));

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> FindFreeCadCommandsInProgramFiles()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)).Distinct())
        {
            IEnumerable<string> directories;

            try
            {
                directories = Directory.EnumerateDirectories(root, "FreeCAD*");
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                yield return Path.Combine(directory, "bin", "FreeCADCmd.exe");
            }
        }
    }

    private static async Task<ConversionResult> RunFreeCadScriptAsync(string freeCadCommand, string script, string outputPath, Action<ConversionProgress> progress)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"stl-step-converter-{Guid.NewGuid():N}.py");
        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8);

        try
        {
            EnsureFreeCadConfigDirectories();

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
            var output = new StringBuilder();
            var stdoutTask = ReadFreeCadOutputAsync(process.StandardOutput, output, progress, false);
            var stderrTask = ReadFreeCadOutputAsync(process.StandardError, output, progress, true);

            await process.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            var outputExists = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            var success = process.ExitCode == 0 && outputExists;
            var outputText = output.ToString().Trim();

            if (process.ExitCode == 0 && !outputExists)
            {
                outputText = "FreeCAD finished but did not create the output file." + Environment.NewLine + outputText;
            }

            return new ConversionResult(success, string.IsNullOrWhiteSpace(outputText) ? "FreeCAD did not report details." : outputText);
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

    private static async Task ReadFreeCadOutputAsync(StreamReader reader, StringBuilder output, Action<ConversionProgress> progress, bool isError)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            lock (output)
            {
                output.AppendLine(line);
            }

            if (TryParseProgress(line, out var parsedProgress))
            {
                progress(parsedProgress);
            }
            else if (isError && !string.IsNullOrWhiteSpace(line))
            {
                progress(new ConversionProgress(null, "FreeCAD: " + line.Trim()));
            }
        }
    }

    private static bool TryParseProgress(string line, out ConversionProgress progress)
    {
        const string progressPrefix = "STLSTEP_PROGRESS|";
        const string logPrefix = "STLSTEP_LOG|";

        if (line.StartsWith(progressPrefix, StringComparison.Ordinal))
        {
            var payload = line[progressPrefix.Length..];
            var separatorIndex = payload.IndexOf('|');
            if (separatorIndex > 0 && int.TryParse(payload[..separatorIndex], out var percent))
            {
                progress = new ConversionProgress(Math.Clamp(percent, 0, 100), payload[(separatorIndex + 1)..]);
                return true;
            }
        }

        if (line.StartsWith(logPrefix, StringComparison.Ordinal))
        {
            progress = new ConversionProgress(null, line[logPrefix.Length..]);
            return true;
        }

        progress = default;
        return false;
    }

    private static void EnsureFreeCadConfigDirectories()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            return;
        }

        foreach (var version in new[] { "", "v1-1", "v1-0", "v0-21", "v0-20" })
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(appData, "FreeCAD", version));
            }
            catch
            {
                // FreeCAD may still run with existing configuration or a custom user profile.
            }
        }
    }

    private static string Py(string value) => JsonSerializer.Serialize(value);

    public static string QuoteArgument(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string Quote(string value) => QuoteArgument(value);

}

internal sealed record ConversionResult(bool Success, string ErrorOutput);

internal readonly record struct ConversionProgress(int? Percent, string Message);
