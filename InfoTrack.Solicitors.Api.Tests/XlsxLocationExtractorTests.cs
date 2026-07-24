using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using InfoTrack.Solicitors.Api.Services;
using Xunit;

namespace InfoTrack.Solicitors.Api.Tests;

public class XlsxLocationExtractorTests
{
    [Fact]
    public async Task ExtractLocations_ReturnsSortedUniqueLocations_FromSharedStrings()
    {
        var shared = new List<string> { "Zoo Town", "Alpha City", "beta city", "Alpha City" };

        var header = new List<string> { "Name", "Location" };
        var rows = new List<List<string>>
        {
            new List<string> { "Name", "Location" }, // header row
            new List<string> { "A", "0" },
            new List<string> { "B", "1" },
            new List<string> { "C", "2" },
            new List<string> { "D", "1" }
        };

        var bytes = BuildXlsxBytes(shared, header, rows);

        var port = GetFreePort();
        var url = $"http://localhost:{port}/test.xlsx";

        using var server = new SimpleHttpServer(port, bytes);
        server.Start();

        var list = await XlsxLocationExtractor.ExtractLocationsFromXlsxUrlAsync(url);

        // expect unique, case-insensitive sort: Alpha City, beta city, Zoo Town
        Assert.Equal(new[] { "Alpha City", "beta city", "Zoo Town" }, list);
    }

    [Fact]
    public async Task ExtractLocations_ReturnsEmpty_WhenNoWorksheet()
    {
        // build zip with no xl/worksheets entry
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = za.CreateEntry("xl/sharedStrings.xml");
            using var s = entry.Open();
            using var sw = new StreamWriter(s, Encoding.UTF8, leaveOpen: true);
            sw.Write("<sst></sst>");
        }
        var bytes = ms.ToArray();

        var port = GetFreePort();
        var url = $"http://localhost:{port}/test.xlsx";

        using var server = new SimpleHttpServer(port, bytes);
        server.Start();

        var list = await XlsxLocationExtractor.ExtractLocationsFromXlsxUrlAsync(url);

        Assert.Empty(list);
    }

    static byte[] BuildXlsxBytes(List<string> sharedStrings, List<string> headerValues, List<List<string>> rows)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // shared strings
            if (sharedStrings != null && sharedStrings.Count > 0)
            {
                var se = za.CreateEntry("xl/sharedStrings.xml");
                using var sst = se.Open();
                var sstDoc = new XDocument(new XElement("sst", sharedStrings.Select(ss => new XElement("si", new XElement("t", ss)))));
                using var tw = new StreamWriter(sst, Encoding.UTF8, leaveOpen: true);
                sstDoc.Save(tw);
            }

            // worksheet
            var we = za.CreateEntry("xl/worksheets/sheet1.xml");
            using var ws = we.Open();
            var sheet = new XElement("worksheet",
                new XElement("sheetData",
                    rows.Select((rowVals, rowIdx) =>
                    {
                        var rowNumber = rowIdx + 1;
                        var row = new XElement("row", new XAttribute("r", rowNumber));
                        for (int colIdx = 0; colIdx < rowVals.Count; colIdx++)
                        {
                            var colLetter = ColLetter(colIdx);
                            var val = rowVals[colIdx];
                            XElement cell;
                            // if this is a header row (rowIdx==0) write inline v
                            if (rowIdx == 0)
                            {
                                cell = new XElement("c", new XAttribute("r", colLetter + rowNumber), new XElement("v", val));
                            }
                            else
                            {
                                // for data rows treat value as shared string index if numeric
                                if (int.TryParse(val, out var idx))
                                {
                                    cell = new XElement("c", new XAttribute("r", colLetter + rowNumber), new XAttribute("t", "s"), new XElement("v", val));
                                }
                                else
                                {
                                    cell = new XElement("c", new XAttribute("r", colLetter + rowNumber), new XElement("v", val));
                                }
                            }
                            row.Add(cell);
                        }
                        return row;
                    })
                )
            );
            var doc = new XDocument(sheet);
            using var tws = new StreamWriter(ws, Encoding.UTF8, leaveOpen: true);
            doc.Save(tws);
        }
        return ms.ToArray();
    }

    static string ColLetter(int idx)
    {
        // simple A, B, C ...
        return ((char)('A' + idx)).ToString();
    }

    static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    class SimpleHttpServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly byte[] _bytes;
        private Task? _serveTask;

        public SimpleHttpServer(int port, byte[] bytes)
        {
            _bytes = bytes;
            _listener.Prefixes.Add($"http://localhost:{port}/");
        }

        public void Start()
        {
            _listener.Start();
            _serveTask = Task.Run(async () =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var ctx = await _listener.GetContextAsync();
                        ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        ctx.Response.ContentLength64 = _bytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(_bytes, 0, _bytes.Length);
                        ctx.Response.OutputStream.Close();
                    }
                }
                catch (HttpListenerException) { }
                catch (ObjectDisposedException) { }
            });
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch { }
            try
            {
                if (_serveTask != null)
                {
                    // Wait briefly for the background serve task to complete after stopping the listener
                    if (!_serveTask.Wait(1000))
                    {
                        // If it didn't complete within the timeout, do not attempt to dispose
                        return;
                    }

                    if (_serveTask.IsCompleted)
                    {
                        _serveTask.Dispose();
                    }
                }
            }
            catch { }
        }
    }
}
