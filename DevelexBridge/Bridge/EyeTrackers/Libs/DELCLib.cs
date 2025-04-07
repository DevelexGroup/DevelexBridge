// Decompiled with JetBrains decompiler
// Type: eyelogic.ELCLib
// Assembly: ELCsApi, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3C74D75E-7DC8-45E8-BA34-03EE7415E6FD
// Assembly location: D:\Develex\develex-bridge\DevelexBridge\Bridge\EyeTrackers\Libs\ELCsApi.dll

using System;
using System.Runtime.InteropServices;

#nullable disable
namespace eyelogic
{
  internal class DELCLib
  {
    private const string dllName = "ELCApi.dll";
    private const string dllPath = "";
    public static double InvalidValue = double.MinValue;

    [DllImport("ELCApi.dll", EntryPoint = "elInitApi", CallingConvention = CallingConvention.StdCall)]
    public static extern int initApi(string clientName);

    [DllImport("ELCApi.dll", EntryPoint = "elDestroyApi", CallingConvention = CallingConvention.StdCall)]
    public static extern void destroyApi();

    [DllImport("ELCApi.dll", EntryPoint = "elRegisterDeviceEventCallback", CallingConvention = CallingConvention.StdCall)]
    public static extern void registerDeviceEventCallback(MulticastDelegate callbackFunction);
    
    [DllImport("ELCApi.dll", EntryPoint = "elRegisterGazeEventCallback", CallingConvention = CallingConvention.StdCall)]
    public static extern void registerGazeEventCallback(MulticastDelegate callbackStartFunction, MulticastDelegate callbackEndFunction);

    [DllImport("ELCApi.dll", EntryPoint = "elRegisterGazeSampleCallback", CallingConvention = CallingConvention.StdCall)]
    public static extern void registerGazeSampleCallback(MulticastDelegate callbackFunction);

    [DllImport("ELCApi.dll", EntryPoint = "elRegisterEyeImageCallback", CallingConvention = CallingConvention.StdCall)]
    public static extern void registerEyeImageCallback(MulticastDelegate callbackFunction);

    [DllImport("kernel32.dll")]
    public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

    [DllImport("ELCApi.dll", EntryPoint = "elConnect", CallingConvention = CallingConvention.StdCall)]
    public static extern int connect();

    [DllImport("ELCApi.dll", EntryPoint = "elConnectRemote", CallingConvention = CallingConvention.StdCall)]
    public static extern int connectRemote(DELCLib.ServerInfo serverInfo);

    [DllImport("ELCApi.dll", EntryPoint = "elRequestServerList", CallingConvention = CallingConvention.StdCall)]
    public static extern int requestServerList(
      int blockingDurationMS,
      [Out] DELCLib.ServerInfo[] serverList,
      int serverListLength);

    [DllImport("ELCApi.dll", EntryPoint = "elDisconnect", CallingConvention = CallingConvention.StdCall)]
    public static extern void disconnect();

    [DllImport("ELCApi.dll", EntryPoint = "elIsConnected", CallingConvention = CallingConvention.StdCall)]
    public static extern bool isConnected();

    [DllImport("ELCApi.dll", EntryPoint = "elGetActiveScreen", CallingConvention = CallingConvention.StdCall)]
    public static extern void getActiveScreen([In, Out] ref DELCLib.ScreenConfig screenConfig);

    [DllImport("ELCApi.dll", EntryPoint = "elGetAvailableScreens", CallingConvention = CallingConvention.StdCall)]
    public static extern int getAvailableScreens(
      [Out] DELCLib.ScreenConfig[] screenConfig,
      int numScreenConfigs);

    [DllImport("ELCApi.dll", EntryPoint = "elSetActiveScreen", CallingConvention = CallingConvention.StdCall)]
    public static extern int setActiveScreen(string screenID, DELCLib.DeviceGeometry deviceGeometry);

    [DllImport("ELCApi.dll", EntryPoint = "elGetDeviceConfig", CallingConvention = CallingConvention.StdCall)]
    public static extern void getDeviceConfig([In, Out] ref DELCLib.DeviceConfig deviceConfig);

    [DllImport("ELCApi.dll", EntryPoint = "elStreamEyeImages", CallingConvention = CallingConvention.StdCall)]
    public static extern int streamEyeImages(bool enable);

    [DllImport("ELCApi.dll", EntryPoint = "elRequestTracking", CallingConvention = CallingConvention.StdCall)]
    public static extern int requestTracking(int frameRateModeInd);

    [DllImport("ELCApi.dll", EntryPoint = "elUnrequestTracking", CallingConvention = CallingConvention.StdCall)]
    public static extern void unrequestTracking();

    [DllImport("ELCApi.dll", EntryPoint = "elCalibrate", CallingConvention = CallingConvention.StdCall)]
    public static extern int calibrate(int calibrationModeInd);

    [DllImport("ELCApi.dll", EntryPoint = "elAbortCalibValidation", CallingConvention = CallingConvention.StdCall)]
    public static extern void abortCalibValidation();

    [DllImport("ELCApi.dll", EntryPoint = "elValidate", CallingConvention = CallingConvention.StdCall)]
    public static extern int validate(ref DELCLib.ValidationResult validationsData);

    [Serializable]
    public struct GazeSample
    {
      public long timestampMicroSec;
      public int index;
      public double porRawX;
      public double porRawY;
      public double porFilteredX;
      public double porFilteredY;
      public double porLeftX;
      public double porLeftY;
      public double eyePositionLeftX;
      public double eyePositionLeftY;
      public double eyePositionLeftZ;
      public double pupilRadiusLeft;
      public double porRightX;
      public double porRightY;
      public double eyePositionRightX;
      public double eyePositionRightY;
      public double eyePositionRightZ;
      public double pupilRadiusRight;
    }
    
    [Serializable]
    public struct ELFixationStart
    {
      public long timestampMicroSec;
      public int index;
      public double porX;
      public double porY;
    }
    
    [Serializable]
    public struct ELFixationStop
    {
      public long timestampMicroSec;
      public long timestampStartMicroSec;
      public int index;
      public int indexStart;
      public double porX;
      public double porY;
    }

    [Serializable]
    public struct EyeImage
    {
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 81000)]
      public byte[] data;
    }

    [Serializable]
    public struct ServerInfo
    {
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
      public byte[] ip;
      public ushort port;
    }

    [Serializable]
    public struct ScreenConfig
    {
      [MarshalAs(UnmanagedType.I1)]
      public bool localMachine;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] id;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] name;
      public int resolutionX;
      public int resolutionY;
      public double physicalSizeX_mm;
      public double physicalSizeY_mm;
    }

    [Serializable]
    public struct DeviceGeometry
    {
      public double mmBelowScreen;
      public double mmTrackerInFrontOfScreen;
    }

    [Serializable]
    public struct DeviceConfig
    {
      public ulong deviceSerial;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
      public byte[] deviceName;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
      public byte[] brandedName;
      [MarshalAs(UnmanagedType.I1)]
      public bool isDemoDevice;
      public int numFrameRates;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
      public byte[] frameRates;
      public int numCalibrationMethods;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
      public byte[] calibrationMethods;
    }

    public struct ValidationPointResult
    {
      public double validationPointPxX;
      public double validationPointPxY;
      public double meanDeviationLeftPx;
      public double meanDeviationLeftDeg;
      public double meanDeviationRightPx;
      public double meanDeviationRightDeg;
    }

    public struct ValidationResult
    {
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
      public DELCLib.ValidationPointResult[] pointsData;
    }
  }
}
