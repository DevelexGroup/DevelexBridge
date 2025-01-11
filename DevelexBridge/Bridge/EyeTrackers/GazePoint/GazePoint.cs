using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Extensions;
using Bridge.Models;

namespace Bridge.EyeTrackers.GazePoint;

public class GazePoint(Func<object, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<object, Task> WsResponse { get; init; } = wsResponse;
    public override DateTime? LastCalibration  { get; set; } = null;
    
    private TcpClient? _tpcClient = null;
    private NetworkStream? _dataFeeder = null;
    private StreamWriter? _dataWriter = null;
    private Thread? _thread = null;
    private CancellationTokenSource _threadCancel = new();
    private readonly SemaphoreSlim _calibrateLock = new(1, 1);
    
    public override async Task<bool> Connect()
    {
        State = EyeTrackerState.Connecting;
        
        try
        {
            _tpcClient = new TcpClient("127.0.0.1", 4242);
        }
        catch (Exception ex)
        {
            _tpcClient = null;
            State = EyeTrackerState.Disconnected;
            throw;
        }

        _dataFeeder = _tpcClient.GetStream();
        _dataWriter = new StreamWriter(_dataFeeder);

        State = EyeTrackerState.Connected;

        await Task.Delay(1);

        return true;
    }

    public override async Task<bool> Start()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        await ToggleSendingData(true);
        await _dataWriter.FlushAsync();
        
        _threadCancel = new();
        _thread = new Thread(DataThread);
        _thread.IsBackground = true;
        _thread.Start();

        State = EyeTrackerState.Started;

        return true;
    }

    public override async Task<bool> Stop()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }

        await ToggleSendingData(false);
        await _dataWriter.FlushAsync();

        if (_thread != null)
        {
            _threadCancel.Cancel();
            _thread.Join();
            _thread = null;
            _threadCancel.Dispose();
        }

        State = EyeTrackerState.Connected;

        return true;
    }

    public override async Task<bool> Calibrate()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }

        State = EyeTrackerState.Calibrating;
        
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_SHOW\" STATE=\"1\" />\r\n");
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_START\" STATE=\"1\" />\r\n");
        await _dataWriter.FlushAsync();
        
        var timeout = TimeSpan.FromSeconds(5);

        var buffer = new byte[1024];
        var successfullySent = false;

        try
        {
            while (true)
            {
                var cancellationTokenSource = new CancellationTokenSource(timeout);
                var cancellationToken = cancellationTokenSource.Token;
                
                await _calibrateLock.WaitAsync(cancellationToken);

                try
                {
                    var readTask = _dataFeeder.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (await Task.WhenAny(readTask, Task.Delay(timeout, cancellationToken)) == readTask)
                    {
                        var bytesRead = await readTask;
                        
                        if (bytesRead <= 0) break;

                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        if (data.Contains("<CAL ID=\"CALIB_RESULT\""))
                        {
                            successfullySent = true;
                            DecodeData(data);
                            
                            break;
                        }
                    }
                    else
                    {
                        throw new TimeoutException("calibration time limit exceeded");
                    }
                }
                finally
                {
                    _calibrateLock.Release();
                    cancellationTokenSource.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is TimeoutException || ex is OperationCanceledException)
        {
            successfullySent = false;
        }
        
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_SHOW\" STATE=\"0\" />\r\n");
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_START\" STATE=\"0\" />\r\n");
        await _dataWriter.FlushAsync();

        State = EyeTrackerState.Connected;

        return successfullySent;
    }

    public override async Task<bool> Disconnect()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        _dataFeeder.Close();
        _dataWriter.Close();
        _tpcClient.Close();

        State = EyeTrackerState.Disconnected;

        await Task.Delay(1);

        return true;
    }

    private async void DataThread()
    {
        while (!_threadCancel.IsCancellationRequested)
        {
            if (State != EyeTrackerState.Started || _dataFeeder == null) continue;
            
            var buffer = new byte[1024];

            try
            {
                var bytesRead = await _dataFeeder.ReadAsync(buffer, 0, buffer.Length, _threadCancel.Token);

                if (bytesRead <= 0) continue;

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Console.WriteLine($"Received: {data}");

                DecodeData(data);
            }
            catch (Exception ex)
            {
            }
        }
    }

    private void DecodeData(string data)
    {
        var lines = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        var keyValueData = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            if (!line.StartsWith("<REC")) continue;
            
            var parts = line
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !part.StartsWith("<REC") && !part.StartsWith("/>"))
                .ToArray();

            if (parts.Length <= 0) continue;
            
            foreach (var part in parts)
            {
                var keyValue = part.Split("=", StringSplitOptions.RemoveEmptyEntries);

                if (keyValue.Length == 2)
                {
                    keyValueData[keyValue[0]] = keyValue[1].Trim('"');
                }
            }
            
            var parsedData = ParseData(keyValueData);
            
            WsResponse(parsedData);
        }
    }

    private WsOutgoingGazeMessage ParseData(Dictionary<string, string> data)
    {
        var outputData = new WsOutgoingGazeMessage
        {
            LeftX = data.Get("LPOGX", "0").ParseDouble(),
            LeftY = data.Get("LPOGY", "0").ParseDouble(),
            RightX = data.Get("RPOGX", "0").ParseDouble(),
            RightY = data.Get("RPOGY", "0").ParseDouble(),
            LeftValidity = data.Get("LPOGV", "0") == "1",
            RightValidity = data.Get("RPOGV", "0") == "1",
            LeftPupil = data.Get("LPD", "0").ParseDouble(),
            RightPupil = data.Get("RPD", "0").ParseDouble(),
            Timestamp = DateTimeExtensions.IsoNow
        };

        if (data.Get("FPOGV", "0") == "1")
        {
            outputData.FixationId = data.Get("FPOGID", "0");
            outputData.FixationDuration = data.Get("FPOGD", "0").ParseInt();
        }

        return outputData;
    }
    
    [MemberNotNullWhen(true, nameof(_tpcClient))]
    [MemberNotNullWhen(true, nameof(_dataFeeder))]
    [MemberNotNullWhen(true, nameof(_dataWriter))]
    private bool IsConnected()
    {
        return _tpcClient != null && _dataFeeder != null && _dataWriter != null;
    }

    private async Task ToggleSendingData(bool state)
    {
        var stateValue = state ? 1 : 0;
        
        if (_dataWriter != null)
        {
            await _dataWriter.WriteAsync($"<SET ID=\"ENABLE_SEND_POG_FIX\" STATE=\"{stateValue}\" />\r\n");
            await _dataWriter.WriteAsync($"<SET ID=\"ENABLE_SEND_POG_LEFT\" STATE=\"{stateValue}\" />\r\n");
            await _dataWriter.WriteAsync($"<SET ID=\"ENABLE_SEND_PUPIL_LEFT\" STATE=\"{stateValue}\" />\r\n");
            await _dataWriter.WriteAsync($"<SET ID=\"ENABLE_SEND_POG_RIGHT\" STATE=\"{stateValue}\" />\r\n");
            await _dataWriter.WriteAsync($"<SET ID=\"ENABLE_SEND_PUPIL_RIGHT\" STATE=\"{stateValue}\" />\r\n");
            await _dataWriter.WriteAsync($"<SET ID=\"ENABLE_SEND_DATA\" STATE=\"{stateValue}\" />\r\n");
        }
    }
}