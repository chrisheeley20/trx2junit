// (c) gfoidl, all rights reserved

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using gfoidl.Trx2Junit.Core.Abstractions;
using gfoidl.Trx2Junit.Core.Internal;

namespace gfoidl.Trx2Junit.Core;

/// <summary>
/// The worker that acts as entry for the conversion of test-files.
/// </summary>
public class Worker
{
    private static readonly Encoding s_utf8 = new UTF8Encoding(false);

    private readonly IFileSystem  _fileSystem;
    private readonly IGlobHandler _globHandler;
    //-------------------------------------------------------------------------
    /// <summary>
    /// Creates a new instance of <see cref="Worker"/>.
    /// </summary>
    /// <param name="fileSystem">
    /// Implementation of the <see cref="IFileSystem"/>.
    /// When <c>null</c> is passed, an default internal implementation is used.
    /// </param>
    /// <param name="globHandler">
    /// Implementation of the <see cref="IGlobHandler"/>.
    /// When <c>null</c> is passed, an default internal implementation is used.
    /// </param>
    public Worker(IFileSystem? fileSystem = null, IGlobHandler? globHandler = null)
    {
        _fileSystem  = fileSystem  ?? new FileSystem();
        _globHandler = globHandler ?? new GlobHandler(_fileSystem);
    }
    //-------------------------------------------------------------------------
    /// <summary>
    /// Executes the conversion of test-files asynchronously.
    /// </summary>
    /// <param name="options">The <see cref="WorkerOptions"/> to use for the conversion.</param>
    /// <returns>A task that completes when the conversion is finished.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is <c>null</c>.
    /// </exception>
    public async Task RunAsync(WorkerOptions options)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(options);
#else
        if (options is null) throw new ArgumentNullException(nameof(options));
#endif

        Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        _globHandler.ExpandWildcards(options);

        ITestResultXmlConverter converter;

        if (options.ConvertToJunit)
        {
            converter = new Trx2JunitTestResultXmlConverter();
            Console.WriteLine($"Converting {options.InputFiles.Count} trx file(s) to JUnit-xml...");
        }
        else
        {
            converter = new Junit2TrxTestResultXmlConverter();
            Console.WriteLine($"Converting {options.InputFiles.Count} junit file(s) to trx-xml...");
        }
        DateTime start = DateTime.Now;

        await Task.WhenAll(options.InputFiles.Select(input => this.ConvertAsync(converter, input, options.OutputDirectory)));

        Console.WriteLine($"done in {(DateTime.Now - start).TotalSeconds} seconds. bye.");
    }
    //-------------------------------------------------------------------------
    // internal for testing
    internal async Task ConvertAsync(ITestResultXmlConverter converter, string inputFile, string? outputPath = null)
    {
        string outputFile = converter.GetOutputFile(inputFile, outputPath);
        this.EnsureOutputDirectoryExists(outputFile);

        Console.WriteLine($"Converting '{inputFile}' to '{outputFile}'");

        using Stream input      = _fileSystem.OpenRead(inputFile);
        using TextWriter output = new StreamWriter(outputFile, false, s_utf8);

        try
        {
            await converter.ConvertAsync(input, output);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.Message);
            Console.ResetColor();

            Environment.ExitCode = 1;
        }
    }
    //-------------------------------------------------------------------------
    private void EnsureOutputDirectoryExists(string outputFile)
    {
        string? directory = Path.GetDirectoryName(outputFile);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            _fileSystem.CreateDirectory(directory);
    }
}