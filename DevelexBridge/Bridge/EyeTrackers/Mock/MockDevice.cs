using System.Diagnostics;
using Bridge.Output;

namespace Bridge.EyeTrackers.Mock;

// ──────────────────────────────────────────────────────────────────
//  Mock data types — mirrors of the eyelogic SDK types
// ──────────────────────────────────────────────────────────────────

public enum MockDeviceEventType
{
    CONNECTION_CLOSED,
    DEVICE_CONNECTED,
    DEVICE_DISCONNECTED,
    TRACKING_STOPPED,
    SCREEN_CHANGED,
}

public class MockPoint2d(double x, double y)
{
    public double x = x;
    public double y = y;
}

public class MockGazeSample
{
    public long timestampMicroSec;
    public int index;
    public MockPoint2d porLeft = new(0, 0);
    public MockPoint2d porRight = new(0, 0);
    public double pupilRadiusLeft;
    public double pupilRadiusRight;
}

public class MockFixationStartSample
{
    public long timestampMicroSec;
    public int index;
    public MockPoint2d por = new(0, 0);
}

public class MockFixationEndSample
{
    public long timestampMicroSec;
    public long timestampStartMicroSec;
    public int index;
    public int indexStart;
    public MockPoint2d por = new(0, 0);
}

// ──────────────────────────────────────────────────────────────────
//  MockDevice — fake hardware API that mirrors DELCsApi
//  Fires gaze / fixation events at 120 Hz on its own thread,
//  exactly like a real eye tracker would.
// ──────────────────────────────────────────────────────────────────

public class MockDevice
{
    // ── Events (same shape as DELCsApi) ────────────────────────────
    public event Action<MockDeviceEventType>? OnDeviceEvent;
    public event Action<MockGazeSample>? OnGazeSample;
    public event Action<MockFixationStartSample>? OnFixationStartSample;
    public event Action<MockFixationEndSample>? OnFixationEndSample;

    private volatile bool _inited;
    private volatile bool _connected;
    private volatile bool _tracking;

    private Thread? _emitterThread;
    private CancellationTokenSource _emitterCancel = new();
    private int _sampleIndex;

    /// <summary>120 Hz ≈ 8.3333 ms per gaze sample.</summary>
    private const double GazeIntervalMs = 1000.0 / 120.0;

    /// <summary>Simulated fixation every ~300 ms (≈ 3.3 Hz).</summary>
    private const int FixationEveryNSamples = 36; // 36 × 8.33 ms ≈ 300 ms

    /// <summary>Simulated fixation duration in microseconds (~250 ms).</summary>
    private const long FixationDurationMicroSec = 250_000;

    // ── Nested config types (mirrors DELCsApi.ScreenConfig / DeviceConfig) ─

    public class ScreenConfig
    {
        public bool localMachine;
        public string id = "";
        public string name = "";
        public int resolutionX;
        public int resolutionY;
        public double physicalSizeX_mm;
        public double physicalSizeY_mm;
    }

    public class DeviceConfig
    {
        public ulong deviceSerial;
        public string deviceName = "";
        public string brandedName = "";
        public bool isDemoDevice;
        public List<int> frameRates = [];
        public List<int> calibrationMethods = [];
    }

    // ── Constructor / lifecycle (mirrors DELCsApi) ─────────────────

    public MockDevice(string clientName)
    {
        _inited = true;
        ConsoleOutput.MockEvent($"MockDevice initialized (client: {clientName})");
    }

    public void connect()
    {
        ThrowIfNotInited("cannot connect");
        _connected = true;
        OnDeviceEvent?.Invoke(MockDeviceEventType.DEVICE_CONNECTED);
    }

    public void disconnect()
    {
        ThrowIfNotInited("cannot disconnect");
        StopEmitterThread();
        _tracking = false;
        _connected = false;
    }

    public bool isConnected() => _inited && _connected;

    public void destroy()
    {
        if (!_inited) return;
        StopEmitterThread();
        _inited = false;
        _connected = false;
        _tracking = false;
    }

    // ── Device queries ─────────────────────────────────────────────

    public DeviceConfig? getDeviceConfig()
    {
        ThrowIfNotInited("cannot get device config");
        return new DeviceConfig
        {
            deviceSerial = 0xDEADBEEF,
            deviceName = "MockDevice",
            brandedName = "Mock Eye Tracker 120 Hz",
            isDemoDevice = true,
            frameRates = [120],
            calibrationMethods = [0],
        };
    }

    public ScreenConfig? getActiveScreen()
    {
        ThrowIfNotInited("cannot get active screen");
        // Use the primary screen resolution so normalisation in the consumer matches reality
        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        return new ScreenConfig
        {
            localMachine = true,
            id = "MOCK-SCREEN-0",
            name = "MockScreen",
            resolutionX = bounds.Width,
            resolutionY = bounds.Height,
            physicalSizeX_mm = 530,
            physicalSizeY_mm = 300,
        };
    }

    // ── Tracking ───────────────────────────────────────────────────

    public void requestTracking(int frameRateModeInd)
    {
        ThrowIfNotInited("cannot request tracking");
        if (!_connected) throw new InvalidOperationException("MockDevice: not connected");
        if (_tracking) return; // already tracking — no-op like real device
        _tracking = true;
        StartEmitterThread();
    }

    public void unrequestTracking()
    {
        ThrowIfNotInited("cannot unrequest tracking");
        StopEmitterThread();
        _tracking = false;
    }

    // ── Calibration ────────────────────────────────────────────────

    public void calibrate(int calibrationModeInd)
    {
        ThrowIfNotInited("cannot calibrate");
        if (!_connected) throw new InvalidOperationException("MockDevice: not connected");
        // Simulate a blocking calibration like the real API
        Thread.Sleep(500);
    }

    // ── Emitter thread ─────────────────────────────────────────────

    private void StartEmitterThread()
    {
        _emitterCancel = new CancellationTokenSource();
        _emitterThread = new Thread(EmitterLoop)
        {
            IsBackground = true,
            Name = "MockDevice-EmitterThread",
            Priority = ThreadPriority.Highest,
        };
        _emitterThread.Start();
    }

    private void StopEmitterThread()
    {
        _emitterCancel.Cancel();
        if (_emitterThread != null)
        {
            _emitterThread.Join(timeout: TimeSpan.FromSeconds(3));
            _emitterThread = null;
            _emitterCancel.Dispose();
        }
    }

    /// <summary>
    /// High-precision emitter loop. Fires <see cref="OnGazeSample"/> at 120 Hz
    /// and periodically fires <see cref="OnFixationStartSample"/> /
    /// <see cref="OnFixationEndSample"/> to simulate fixation detection.
    /// Uses Stopwatch + hybrid sleep / spin-wait for sub-ms precision.
    /// </summary>
    private void EmitterLoop()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var nextTickMs = sw.Elapsed.TotalMilliseconds;
            var epoch = DateTimeOffset.UtcNow;
            int localIndex = 0;

            // Fixation tracking state
            int fixationStartIndex = -1;
            long fixationStartMicroSec = 0;

            while (!_emitterCancel.Token.IsCancellationRequested)
            {
                var nowMs = sw.Elapsed.TotalMilliseconds;

                if (nowMs >= nextTickMs)
                {
                    localIndex = Interlocked.Increment(ref _sampleIndex);
                    var timestampMicroSec = (long)(nowMs * 1000.0);

                    // ── Gaze sample ────────────────────────────────
                    OnGazeSample?.Invoke(new MockGazeSample
                    {
                        timestampMicroSec = timestampMicroSec,
                        index = localIndex,
                        porLeft = new MockPoint2d(0, 0),
                        porRight = new MockPoint2d(0, 0),
                        pupilRadiusLeft = 3.5,
                        pupilRadiusRight = 3.5,
                    });

                    // ── Fixation simulation ────────────────────────
                    if (localIndex % FixationEveryNSamples == 0)
                    {
                        // End the previous fixation if one is active
                        if (fixationStartIndex >= 0)
                        {
                            OnFixationEndSample?.Invoke(new MockFixationEndSample
                            {
                                timestampMicroSec = timestampMicroSec,
                                timestampStartMicroSec = fixationStartMicroSec,
                                index = localIndex,
                                indexStart = fixationStartIndex,
                                por = new MockPoint2d(0, 0),
                            });
                        }

                        // Start a new fixation
                        fixationStartIndex = localIndex;
                        fixationStartMicroSec = timestampMicroSec;

                        OnFixationStartSample?.Invoke(new MockFixationStartSample
                        {
                            timestampMicroSec = timestampMicroSec,
                            index = localIndex,
                            por = new MockPoint2d(0, 0),
                        });
                    }

                    nextTickMs += GazeIntervalMs;

                    // If we've fallen behind, skip ahead instead of bursting
                    if (nextTickMs < nowMs)
                        nextTickMs = nowMs + GazeIntervalMs;
                }
                else
                {
                    var remainingMs = nextTickMs - nowMs;
                    if (remainingMs > 2.0)
                        Thread.Sleep(1);
                    else
                        Thread.SpinWait(100);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private void ThrowIfNotInited(string action)
    {
        if (!_inited)
            throw new InvalidOperationException($"MockDevice: {action} — not initialized or already destroyed");
    }
}

