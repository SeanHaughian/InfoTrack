using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

Process? npmProcess = null;

if (env.IsDevelopment())
{
    // Locate package.json in project tree
    string projectDir = Directory.GetCurrentDirectory();
    for (int i = 0; i < 8; i++)
    {
        if (File.Exists(Path.Combine(projectDir, "package.json"))) break;
        var parent = Directory.GetParent(projectDir);
        if (parent == null) break;
        projectDir = parent.FullName;
    }

    try
    {
        string fileName;
        string arguments;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName = "cmd";
            arguments = "/c npm run dev";
        }
        else
        {
            fileName = "npm";
            arguments = "run dev";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        npmProcess = Process.Start(startInfo);

        if (npmProcess != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await npmProcess.StandardOutput.ReadLineAsync()) != null)
                    {
                        Console.WriteLine(line);
                    }
                }
                catch { }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await npmProcess.StandardError.ReadLineAsync()) != null)
                    {
                        Console.Error.WriteLine(line);
                    }
                }
                catch { }
            });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Failed to start npm dev server: " + ex.Message);
    }
}

var app = builder.Build();

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        if (npmProcess != null && !npmProcess.HasExited)
        {
            npmProcess.Kill(entireProcessTree: true);
        }
    }
    catch { }
});

// Redirect root to Vite dev server (adjust port if your dev server prints a different one)
app.MapGet("/", () => Results.Redirect("http://localhost:50954"));

app.Run();
