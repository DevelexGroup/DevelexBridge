using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Models;

namespace Bridge.EyeTrackers.OpenGaze;

public class OpenGaze(Func<WsBaseResponseMessage, Task> wsResponse) : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    public override Func<WsBaseResponseMessage, Task> WsResponse { get; init; } = wsResponse;
    
    private TcpClient? _tpcClient = null;
    private NetworkStream? _dataFeeder = null;
    private StreamWriter? _dataWriter = null;
    private Thread? _thread = null;
    private bool _isRunning = false;
    
    public override async void Connect()
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

        await WsResponse(new WsBaseResponseMessage("connected"));
    }

    public override async void Start()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_POG_FIX\" STATE=\"1\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_POG_LEFT\" STATE=\"1\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_POG_RIGHT\" STATE=\"1\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_PUPIL_LEFT\" STATE=\"1\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_PUPIL_RIGHT\" STATE=\"1\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_DATA\" STATE=\"1\" />\\r\\n");
        await _dataWriter.FlushAsync();
        
        _isRunning = true;
        _thread = new Thread(DataThread);
        _thread.IsBackground = true;
        _thread.Start();

        State = EyeTrackerState.Started;

        await WsResponse(new WsBaseResponseMessage("started"));
    }

    public override async void Stop()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_POG_FIX\" STATE=\"0\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_POG_LEFT\" STATE=\"0\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_POG_RIGHT\" STATE=\"0\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_PUPIL_LEFT\" STATE=\"0\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_PUPIL_RIGHT\" STATE=\"0\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_DATA\" STATE=\"0\" />\\r\\n");
        await _dataWriter.FlushAsync();

        if (_thread != null)
        {
            _isRunning = false;
            _thread.Join();
            _thread = null;
        }

        State = EyeTrackerState.Stopped;

        await WsResponse(new WsBaseResponseMessage("stopped"));
    }

    public override async void Calibrate()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_SHOW\" STATE=\"1\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_START\" STATE=\"1\" />\\r\\n");
        await _dataWriter.FlushAsync();

        byte[] buffer = new byte[1024];
        int bytesRead;
        
        // TODO: timeout
        while ((bytesRead = await _dataFeeder.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            Console.WriteLine($"Received: {data}");

            if (data.Contains("<CAL ID=\"CALIB_RESULT\""))
            {
                break;
            }
        }
        
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_SHOW\" STATE=\"0\" />\\r\\n");
        await _dataWriter.WriteAsync("<SET ID=\"CALIBRATE_START\" STATE=\"0\" />\\r\\n");
        await _dataWriter.FlushAsync();

        await WsResponse(new WsBaseResponseMessage("calibrated"));
    }

    public override async void Disconnect()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        _dataFeeder.Close();
        _dataWriter.Close();
        _tpcClient.Close();

        State = EyeTrackerState.Disconnected;
        
        await WsResponse(new WsBaseResponseMessage("disconnected"));
    }

    private async void DataThread()
    {
        while (_isRunning)
        {
            if (State == EyeTrackerState.Started && _dataFeeder != null)
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = await _dataFeeder.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Console.WriteLine($"Received: {data}");

                    DecodeData(data);
                }
            }
        }
    }

    private void DecodeData(string data)
    {
        var lines = data.Split("\n").Where(s => !string.IsNullOrEmpty(s)).ToList();
        var keyValueData = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            if (!line.StartsWith("<REC"))
            {
                continue;
            }
            
            var parts = line.Split(" ").Where(s => !string.IsNullOrEmpty(s)).ToList();

            if (parts.Count < 2)
            {
                continue;
            }

            if (parts[0].StartsWith("<REC"))
            {
                parts.RemoveAt(0);
            }

            int lastIndex = parts.Count - 1;

            if (parts[lastIndex].StartsWith("/>"))
            {
                parts.RemoveAt(lastIndex);
            }

            foreach (var part in parts)
            {
                var keyValue = part.Split("=").Where(s => !string.IsNullOrEmpty(s)).ToList();

                if (keyValue.Count != 2)
                {
                    continue;
                }

                keyValueData[keyValue[0]] = keyValue[1].Trim('"');
            }

            var parsedData = ParseData(keyValueData);
            
            WsResponse(parsedData);
        }
    }

    private WsResponseOutput ParseData(Dictionary<string, string> data)
    {
        var outputData = new WsResponseOutput("point");

        outputData.LeftX = double.Parse(data["xL"]);
        outputData.LeftY = double.Parse(data["yL"]);
        outputData.RightX = double.Parse(data["xR"]);
        outputData.RightY = double.Parse(data["yR"]);
        outputData.LeftValidity = bool.Parse(data["validityL"]);
        outputData.RightValidity = bool.Parse(data["validityR"]);
        outputData.Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        if (data["FPOGV"] == "1")
        {
            outputData.FixationId = data["fixationId"];
            outputData.FixationDuration = int.Parse(data["fixationDuration"]);
        }

        return outputData;
    }
    
    [MemberNotNullWhen(true, nameof(_tpcClient))]
    [MemberNotNullWhen(true, nameof(_dataFeeder))]
    [MemberNotNullWhen(true, nameof(_dataWriter))]
    private bool IsConnected()
    {
        return _tpcClient != null && _tpcClient.Connected && _dataFeeder != null && _dataWriter != null;
    }
}