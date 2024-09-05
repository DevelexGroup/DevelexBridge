using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using Bridge.Enums;
using Bridge.Exceptions.EyeTracker;
using Bridge.Models;

namespace Bridge.EyeTrackers.OpenGaze;

public class OpenGaze : EyeTracker
{
    public override EyeTrackerState State { get; set; } = EyeTrackerState.Disconnected;
    private TcpClient? _tpcClient = null;
    private NetworkStream? _dataFeeder = null;
    private StreamWriter? _dataWriter = null;

    public override async void Connect()
    {
        State = EyeTrackerState.Connecting;
        
        try
        {
            _tpcClient = new TcpClient("127.0.0.1", 4242);
        }
        catch (Exception ex)
        {
            Console.WriteLine("OG cannot connect");
            _tpcClient = null;
            return;
        }

        _dataFeeder = _tpcClient.GetStream();
        _dataWriter = new StreamWriter(_dataFeeder);

        State = EyeTrackerState.Connected;
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
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_DATA\" STATE=\"1\" />\\r\\n");
        await _dataWriter.FlushAsync();

        State = EyeTrackerState.Started;
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
        await _dataWriter.WriteAsync("<SET ID=\"ENABLE_SEND_DATA\" STATE=\"0\" />\\r\\n");
        await _dataWriter.FlushAsync();

        State = EyeTrackerState.Stopped;
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
    }

    public override void Disconnect()
    {
        if (!IsConnected())
        {
            throw new EyeTrackerNotConnected("zařízení není připojené");
        }
        
        _dataFeeder.Close();
        _dataWriter.Close();
        _tpcClient.Close();


        State = EyeTrackerState.Disconnected;
    }
    
    [MemberNotNullWhen(true, nameof(_tpcClient))]
    [MemberNotNullWhen(true, nameof(_dataFeeder))]
    [MemberNotNullWhen(true, nameof(_dataWriter))]
    private bool IsConnected()
    {
        return _tpcClient != null && _tpcClient.Connected && _dataFeeder != null && _dataWriter != null;
    }
}