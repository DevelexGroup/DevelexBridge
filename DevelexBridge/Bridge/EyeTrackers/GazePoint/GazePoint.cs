using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Extensions;
using Bridge.Models;
using Bridge.Output;

namespace Bridge.EyeTrackers.GazePoint;

public class GazePoint(Func<object, bool, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, bool, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration { get; set; }

    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private Task? _readerTask;
    private CancellationTokenSource _readerCancel = new();
    private TaskCompletionSource<bool>? _calibrationTcs;
    private WsOutgoingFixationStartMessage? _latestFixationStartMessage;
    private WsOutgoingFixationEndMessage? _latestFixationEndMessage;

    /// <summary>
    /// Server-reported tick frequency for converting TIME_TICK to seconds (API §3.25).
    /// Defaults to local Stopwatch.Frequency as a fallback.
    /// </summary>
    private long _timeTickFrequency = Stopwatch.Frequency;

    /// <summary>
    /// Maximum time allowed for the entire calibration procedure.
    /// </summary>
    private static readonly TimeSpan CALIBRATION_TIMEOUT = TimeSpan.FromSeconds(60);

    public override async Task<bool> Connect()
    {
        State = EyeTrackerState.Connecting;

        try
        {
            _tcpClient = new TcpClient
            {
                NoDelay = true,            // Disable Nagle's algorithm — critical for low-latency streaming
                ReceiveBufferSize = 65536, // 64 KB receive buffer for high-throughput 150 Hz data
                SendBufferSize = 8192
            };

            await _tcpClient.ConnectAsync("127.0.0.1", 4242,
                new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        }
        catch (Exception)
        {
            _tcpClient?.Dispose();
            _tcpClient = null;
            State = EyeTrackerState.Disconnected;
            throw;
        }

        _networkStream = _tcpClient.GetStream();
        _writer = new StreamWriter(_networkStream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
        {
            AutoFlush = false
        };
        _reader = new StreamReader(_networkStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096, leaveOpen: true);

        // Query device information before starting the reader loop (API §3.25–3.29)
        await QueryDeviceInfo();

        // Start reader immediately — it runs continuously from Connect to Disconnect.
        // It only processes gaze records when State == Started, and watches for
        // calibration results when State == Calibrating.
        // This also prevents the TCP receive buffer from filling up.
        _readerCancel = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReaderLoop(_readerCancel.Token));

        State = EyeTrackerState.Connected;

        ConsoleOutput.GazePointEvent("Connected to GazePoint device on 127.0.0.1:4242");

        return true;
    }

    public override async Task<bool> Start()
    {
        if (!IsConnected())
            throw new EyeTrackerNotConnected("Device is not connected");

        await SendCommands(
            "<SET ID=\"ENABLE_SEND_TIME_TICK\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_COUNTER\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_POG_FIX\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_POG_LEFT\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_PUPIL_LEFT\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_POG_RIGHT\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_PUPIL_RIGHT\" STATE=\"1\" />",
            "<SET ID=\"ENABLE_SEND_DATA\" STATE=\"1\" />"
        );

        State = EyeTrackerState.Started;

        ConsoleOutput.GazePointEvent("Started streaming gaze data");

        return true;
    }

    public override async Task<bool> Stop()
    {
        if (!IsConnected())
            throw new EyeTrackerNotConnected("Device is not connected");

        // Set state BEFORE sending disable commands so the reader loop
        // immediately stops processing new records — minimises latency.
        State = EyeTrackerState.Connected;

        await SendCommands(
            "<SET ID=\"ENABLE_SEND_DATA\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_TIME_TICK\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_COUNTER\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_POG_FIX\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_POG_LEFT\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_PUPIL_LEFT\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_POG_RIGHT\" STATE=\"0\" />",
            "<SET ID=\"ENABLE_SEND_PUPIL_RIGHT\" STATE=\"0\" />"
        );

        // Emit pending fixation end for the last active fixation
        if (_latestFixationEndMessage is not null)
            await WsResponse(_latestFixationEndMessage, false);

        _latestFixationStartMessage = null;
        _latestFixationEndMessage = null;

        ConsoleOutput.GazePointEvent("Stopped streaming gaze data");

        return true;
    }

    public override async Task<bool> Calibrate()
    {
        if (!IsConnected())
            throw new EyeTrackerNotConnected("Device is not connected");

        var wasStarted = State == EyeTrackerState.Started;

        State = EyeTrackerState.Calibrating;

        // Set up a TaskCompletionSource so the reader loop can signal when
        // it receives the CALIB_RESULT message — no race condition since
        // only one task reads the TCP stream.
        _calibrationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await SendCommands(
            "<SET ID=\"CALIBRATE_SHOW\" STATE=\"1\" />",
            "<SET ID=\"CALIBRATE_START\" STATE=\"1\" />"
        );

        ConsoleOutput.GazePointEvent("Calibration started — waiting for result...");

        var success = false;

        try
        {
            success = await _calibrationTcs.Task.WaitAsync(CALIBRATION_TIMEOUT, _readerCancel.Token);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            ConsoleOutput.GazePointEvent($"Calibration failed: {ex.Message}");
        }
        finally
        {
            _calibrationTcs = null;
        }

        await SendCommands(
            "<SET ID=\"CALIBRATE_SHOW\" STATE=\"0\" />",
            "<SET ID=\"CALIBRATE_START\" STATE=\"0\" />"
        );

        // Restore to Started if we were tracking before calibration,
        // otherwise go back to Connected.
        State = wasStarted ? EyeTrackerState.Started : EyeTrackerState.Connected;

        if (success)
        {
            LastCalibration = DateTime.UtcNow;
            ConsoleOutput.GazePointEvent("Calibration completed successfully");
        }

        return success;
    }

    public override async Task<bool> Disconnect()
    {
        if (!IsConnected())
            throw new EyeTrackerNotConnected("Device is not connected");

        // 1. Cancel the reader task and wait for it to finish
        await _readerCancel.CancelAsync();

        if (_readerTask != null)
        {
            try
            {
                await _readerTask.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Reader may already be stopped or stream broken
            }

            _readerTask = null;
        }

        _readerCancel.Dispose();

        // 2. Dispose streams and TCP client (reader/writer use leaveOpen so stream isn't double-closed)
        _reader?.Dispose();
        _writer?.Dispose();
        _networkStream?.Dispose();
        _tcpClient?.Dispose();

        _reader = null;
        _writer = null;
        _networkStream = null;
        _tcpClient = null;

        _latestFixationStartMessage = null;
        _latestFixationEndMessage = null;

        State = EyeTrackerState.Disconnected;

        ConsoleOutput.GazePointEvent("Disconnected from GazePoint device");

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TCP Reader Loop
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Main reader loop that runs continuously from Connect to Disconnect.
    /// Uses <see cref="StreamReader.ReadLineAsync(CancellationToken)"/> for
    /// efficient, allocation-light, line-based TCP reading with correct
    /// framing — no partial messages, no split records.
    /// </summary>
    private async Task ReaderLoop(CancellationToken ct)
    {
        ConsoleOutput.GazePointEvent("Reader loop started");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync(ct);

                if (line == null)
                {
                    ConsoleOutput.GazePointEvent("TCP stream ended — GazePoint Control closed the connection");
                    break;
                }

                // Gaze data record (API §5)
                if (line.StartsWith("<REC"))
                {
                    if (State == EyeTrackerState.Started)
                    {
                        await ProcessRecord(line);
                    }

                    continue;
                }

                // Calibration events (API §4)
                if (line.StartsWith("<CAL"))
                {
                    // Must check for exact ID="CALIB_RESULT" — not just Contains("CALIB_RESULT")
                    // because CALIB_RESULT_PT (per-point result, §4.2) would also match.
                    if (line.Contains("ID=\"CALIB_RESULT\""))
                    {
                        ConsoleOutput.GazePointEvent("Calibration final result received");
                        _calibrationTcs?.TrySetResult(true);
                    }
                    else if (line.Contains("CALIB_START_PT"))
                    {
                        var attrs = ParseAttributes(line);
                        ConsoleOutput.GazePointEvent(
                            $"Calibration point {attrs.Get("PT", "?")} started");
                    }
                    else if (line.Contains("CALIB_RESULT_PT"))
                    {
                        var attrs = ParseAttributes(line);
                        ConsoleOutput.GazePointEvent(
                            $"Calibration point {attrs.Get("PT", "?")} completed");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during Disconnect
        }
        catch (IOException ex)
        {
            ConsoleOutput.GazePointEvent($"Reader loop IO error (connection lost?): {ex.Message}");
        }
        catch (Exception ex)
        {
            ConsoleOutput.GazePointEvent($"Reader loop error: {ex.Message}");
        }

        ConsoleOutput.GazePointEvent("Reader loop stopped");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Record Parsing
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Processes a single &lt;REC .../&gt; line into gaze and fixation messages.
    /// </summary>
    private async Task ProcessRecord(string line)
    {
        var data = ParseAttributes(line);
        if (data.Count == 0) return;

        var parsed = BuildMessages(data);

        await WsResponse(parsed.Gaze, false);

        if (parsed.FixationStart != null)
            await WsResponse(parsed.FixationStart, false);

        if (parsed.FixationEnd != null)
            await WsResponse(parsed.FixationEnd, false);
    }

    /// <summary>
    /// Parses a KEY="VALUE" attribute line (works for &lt;REC&gt;, &lt;CAL&gt;, &lt;ACK&gt;).
    /// Returns a fresh dictionary per call to avoid stale-data bugs.
    /// </summary>
    private static Dictionary<string, string> ParseAttributes(string line)
    {
        var data = new Dictionary<string, string>(16);

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part[0] == '<' || part[0] == '/') continue; // skip <REC and />

            var eqIndex = part.IndexOf('=');
            if (eqIndex > 0 && eqIndex < part.Length - 1)
            {
                var key = part[..eqIndex];
                var value = part[(eqIndex + 1)..].Trim('"');
                data[key] = value;
            }
        }

        return data;
    }

    /// <summary>
    /// Builds gaze and fixation WebSocket messages from parsed record attributes.
    /// </summary>
    private (WsOutgoingGazeMessage Gaze, WsOutgoingFixationStartMessage? FixationStart, WsOutgoingFixationEndMessage? FixationEnd)
        BuildMessages(Dictionary<string, string> data)
    {
        var tickDelta = Stopwatch.GetTimestamp() - data.Get("TIME_TICK", "0").ParseLong();
        var deviceTimestamp = DateTimeExtensions.HighResUtcNow
            .AddMilliseconds(-(tickDelta / (double)_timeTickFrequency * 1000));
        var currentTimestamp = DateTimeExtensions.IsoNow;

        var gaze = new WsOutgoingGazeMessage
        {
            DeviceId = data.Get("CNT", "-1").ParseInt(),
            LeftX = data.Get("LPOGX", "0").ParseDouble(),
            LeftY = data.Get("LPOGY", "0").ParseDouble(),
            RightX = data.Get("RPOGX", "0").ParseDouble(),
            RightY = data.Get("RPOGY", "0").ParseDouble(),
            LeftValidity = data.Get("LPOGV", "0") == "1",
            RightValidity = data.Get("RPOGV", "0") == "1",
            LeftPupil = data.Get("LPD", "0").ParseDouble(),
            RightPupil = data.Get("RPD", "0").ParseDouble(),
            Timestamp = currentTimestamp,
            DeviceTimestamp = deviceTimestamp.ToIso()
        };

        WsOutgoingFixationStartMessage? fixationStart = null;
        WsOutgoingFixationEndMessage? fixationEnd = null;

        if (data.Get("FPOGV", "0") == "1")
        {
            var fixationId = data.Get("FPOGID", "0").ParseInt();
            var gazeDeviceId = data.Get("CNT", "-1").ParseInt();
            var x = data.Get("FPOGX", "0").ParseDouble();
            var y = data.Get("FPOGY", "0").ParseDouble();
            var milliseconds = data.Get("FPOGD", "0").ParseDouble() * 1000;

            if (_latestFixationEndMessage is not null && _latestFixationStartMessage is not null &&
                _latestFixationStartMessage.FixationId != fixationId)
            {
                fixationEnd = _latestFixationEndMessage;
            }

            if (_latestFixationStartMessage is null || _latestFixationStartMessage.FixationId != fixationId)
            {
                _latestFixationStartMessage = fixationStart = new WsOutgoingFixationStartMessage
                {
                    FixationId = fixationId,
                    GazeDeviceId = gazeDeviceId,
                    X = x,
                    Y = y,
                    Duration = milliseconds,
                    Timestamp = currentTimestamp,
                    DeviceTimestamp = deviceTimestamp.AddMilliseconds(-milliseconds).ToIso()
                };
            }

            _latestFixationEndMessage = new WsOutgoingFixationEndMessage
            {
                FixationId = fixationId,
                GazeDeviceId = gazeDeviceId,
                X = x,
                Y = y,
                Duration = milliseconds - _latestFixationStartMessage.Duration,
                Timestamp = currentTimestamp,
                DeviceTimestamp = deviceTimestamp.AddMilliseconds(-milliseconds).ToIso()
            };
        }
        else if (_latestFixationEndMessage is not null)
        {
            // Eyes lost (FPOGV=0) — emit the pending fixation end
            fixationEnd = _latestFixationEndMessage;
            _latestFixationStartMessage = null;
            _latestFixationEndMessage = null;
        }

        return (gaze, fixationStart, fixationEnd);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    [MemberNotNullWhen(true, nameof(_tcpClient))]
    [MemberNotNullWhen(true, nameof(_networkStream))]
    [MemberNotNullWhen(true, nameof(_writer))]
    [MemberNotNullWhen(true, nameof(_reader))]
    private bool IsConnected()
    {
        return _tcpClient != null && _networkStream != null && _writer != null && _reader != null;
    }

    /// <summary>
    /// Sends multiple commands to the GazePoint server in a single flush.
    /// Batching reduces the number of TCP segments on the wire.
    /// </summary>
    private async Task SendCommands(params string[] commands)
    {
        if (_writer == null) return;

        foreach (var cmd in commands)
        {
            await _writer.WriteAsync(cmd);
            await _writer.WriteAsync("\r\n");
        }

        await _writer.FlushAsync();
    }

    /// <summary>
    /// Queries device info from the server on connect (API §3.25–3.30).
    /// Must be called BEFORE starting the reader loop so we can read ACK responses directly.
    /// </summary>
    private async Task QueryDeviceInfo()
    {
        // TIME_TICK_FREQUENCY — needed for converting TIME_TICK to seconds (§3.25)
        var tickFreq = await QueryServer("TIME_TICK_FREQUENCY");
        if (tickFreq != null && tickFreq.TryGetValue("FREQ", out var freqStr) &&
            long.TryParse(freqStr, out var freq) && freq > 0)
        {
            _timeTickFrequency = freq;
            ConsoleOutput.GazePointEvent($"Time tick frequency: {_timeTickFrequency}");
        }
        else
        {
            ConsoleOutput.GazePointEvent(
                $"Could not query TIME_TICK_FREQUENCY — falling back to Stopwatch.Frequency ({Stopwatch.Frequency})");
        }

        // PRODUCT_ID (§3.28)
        var product = await QueryServer("PRODUCT_ID");
        if (product != null)
            ConsoleOutput.GazePointEvent($"Product: {product.Get("VALUE", "unknown")}");

        // SERIAL_ID (§3.29)
        var serial = await QueryServer("SERIAL_ID");
        if (serial != null)
            ConsoleOutput.GazePointEvent($"Serial: {serial.Get("VALUE", "unknown")}");

        // SCREEN_SIZE (§3.26)
        var screen = await QueryServer("SCREEN_SIZE");
        if (screen != null)
            ConsoleOutput.GazePointEvent(
                $"Screen: {screen.Get("WIDTH", "?")}x{screen.Get("HEIGHT", "?")} at ({screen.Get("X", "0")},{screen.Get("Y", "0")})");

        // API_ID (§3.31)
        var api = await QueryServer("API_ID");
        if (api != null)
            ConsoleOutput.GazePointEvent($"API version: {api.Get("VALUE", "unknown")}");
    }

    /// <summary>
    /// Sends a GET query to the server and returns the parsed ACK response attributes.
    /// Must only be called before the reader loop is started (no concurrent reads).
    /// </summary>
    private async Task<Dictionary<string, string>?> QueryServer(string id)
    {
        if (_writer == null || _reader == null) return null;

        await _writer.WriteAsync($"<GET ID=\"{id}\" />\r\n");
        await _writer.FlushAsync();

        // Read lines until we find the ACK for our query (max 10 attempts to skip
        // any interleaved messages from the server)
        for (var i = 0; i < 10; i++)
        {
            var line = await _reader.ReadLineAsync();
            if (line == null) return null;

            if (line.StartsWith("<ACK") && line.Contains($"ID=\"{id}\""))
            {
                return ParseAttributes(line);
            }
        }

        return null;
    }
}