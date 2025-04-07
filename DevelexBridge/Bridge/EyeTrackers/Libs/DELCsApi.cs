// Decompiled with JetBrains decompiler
// Type: eyelogic.ELCsApi
// Assembly: ELCsApi, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 3C74D75E-7DC8-45E8-BA34-03EE7415E6FD
// Assembly location: D:\Develex\develex-bridge\DevelexBridge\Bridge\EyeTrackers\Libs\ELCsApi.dll

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

#nullable disable
namespace eyelogic
{
  public class DELCsApi
  {
    public static double InvalidValue = double.MinValue;
    private bool m_inited;
    private DELCsApi.DeviceEventCallback m_deviceEventCallback;
    private DELCsApi.GazeSampleCallback m_gazeSampleCallback;
    private DELCsApi.EyeImageCallback m_eyeImageCallback;
    private DELCsApi.FixationStartCallback m_fixationStartCallback; 
    private DELCsApi.FixationEndCallback m_fixationEndCallback; 

    public event DELCsApi.ELDeviceEvent OnDeviceEvent = _param1 => { };

    public event DELCsApi.ELGazeSample OnGazeSample = _param1 => { };

    public event DELCsApi.ELEyeImage OnEyeImage = _param1 => { };
    
    public event DELCsApi.ELFixationStartSample OnFixationStartSample = _param1 => { };
    
    public event DELCsApi.ELFixationEndSample OnFixationEndSample = _param1 => { };

    public DELCsApi(string clientName)
    {
      this.m_inited = false;
      switch (DELCLib.initApi(clientName))
      {
        case 0:
          DELCLib.registerDeviceEventCallback((MulticastDelegate) (this.m_deviceEventCallback = new DELCsApi.DeviceEventCallback(this.onDeviceEvent)));
          DELCLib.registerGazeSampleCallback((MulticastDelegate) (this.m_gazeSampleCallback = new DELCsApi.GazeSampleCallback(this.onGazeSample)));
          DELCLib.registerEyeImageCallback((MulticastDelegate) (this.m_eyeImageCallback = new DELCsApi.EyeImageCallback(this.onEyeImage)));
          DELCLib.registerGazeEventCallback((MulticastDelegate) (this.m_fixationStartCallback = new DELCsApi.FixationStartCallback(this.onFixationStartSample)), (MulticastDelegate) (this.m_fixationEndCallback = new DELCsApi.FixationEndCallback(this.onFixationEndSample)));
          this.m_inited = true;
          break;
        case 1:
          throw new ELException(ELException.ErrorType.ALREADY_INITED, "cannot initialize: already initialized before");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "cannot initialize: error cause unknown");
      }
    }

    private static string decodeBytesToString(byte[] bytes, Encoding encoding)
    {
      if (bytes == null)
        return "";
      int count = ((IEnumerable<byte>) bytes).TakeWhile<byte>((Func<byte, bool>) (c => c > (byte) 0)).Count<byte>();
      return encoding.GetString(bytes, 0, count);
    }

    private void onDeviceEvent(int e)
    {
      switch (e)
      {
        case 0:
          DELCsApi.ELDeviceEvent onDeviceEvent1 = this.OnDeviceEvent;
          if (onDeviceEvent1 == null)
            break;
          onDeviceEvent1(DeviceEventType.SCREEN_CHANGED);
          break;
        case 1:
          DELCsApi.ELDeviceEvent onDeviceEvent2 = this.OnDeviceEvent;
          if (onDeviceEvent2 == null)
            break;
          onDeviceEvent2(DeviceEventType.CONNECTION_CLOSED);
          break;
        case 2:
          DELCsApi.ELDeviceEvent onDeviceEvent3 = this.OnDeviceEvent;
          if (onDeviceEvent3 == null)
            break;
          onDeviceEvent3(DeviceEventType.DEVICE_CONNECTED);
          break;
        case 3:
          DELCsApi.ELDeviceEvent onDeviceEvent4 = this.OnDeviceEvent;
          if (onDeviceEvent4 == null)
            break;
          onDeviceEvent4(DeviceEventType.DEVICE_DISCONNECTED);
          break;
        case 4:
          DELCsApi.ELDeviceEvent onDeviceEvent5 = this.OnDeviceEvent;
          if (onDeviceEvent5 == null)
            break;
          onDeviceEvent5(DeviceEventType.TRACKING_STOPPED);
          break;
      }
    }

    private void onGazeSample(ref DELCLib.GazeSample gazeSample)
    {
      GazeSample sample = new GazeSample();
      sample.timestampMicroSec = gazeSample.timestampMicroSec;
      sample.index = gazeSample.index;
      sample.porRaw = new Point2d(gazeSample.porRawX, gazeSample.porRawY);
      sample.porFiltered = new Point2d(gazeSample.porFilteredX, gazeSample.porFilteredY);
      sample.porLeft = new Point2d(gazeSample.porLeftX, gazeSample.porLeftY);
      sample.eyePositionLeft = new Point3d(gazeSample.eyePositionLeftX, gazeSample.eyePositionLeftY, gazeSample.eyePositionLeftZ);
      sample.pupilRadiusLeft = gazeSample.pupilRadiusLeft;
      sample.porRight = new Point2d(gazeSample.porRightX, gazeSample.porRightY);
      sample.eyePositionRight = new Point3d(gazeSample.eyePositionRightX, gazeSample.eyePositionRightY, gazeSample.eyePositionRightZ);
      sample.pupilRadiusRight = gazeSample.pupilRadiusRight;
      DELCsApi.ELGazeSample onGazeSample = this.OnGazeSample;
      if (onGazeSample == null)
        return;
      onGazeSample(sample);
    }
    
    private void onFixationStartSample(ref DELCLib.ELFixationStart elFixationStart)
    {
      var fixationStart = new FixationStartSample();
      fixationStart.timestampMicroSec = elFixationStart.timestampMicroSec;
      fixationStart.index = elFixationStart.index;
      fixationStart.por = new Point2d(elFixationStart.porX, elFixationStart.porY);
      var onFixationStartSample = this.OnFixationStartSample;
      if (onFixationStartSample == null)
        return;
      onFixationStartSample(fixationStart);
    }
    
    private void onFixationEndSample(ref DELCLib.ELFixationStop elFixationStop)
    {
      var fixationEnd = new FixationEndSample();
      fixationEnd.timestampMicroSec = elFixationStop.timestampMicroSec;
      fixationEnd.timestampStartMicroSec = elFixationStop.timestampStartMicroSec;
      fixationEnd.index = elFixationStop.index;
      fixationEnd.indexStart = elFixationStop.indexStart;
      fixationEnd.por = new Point2d(elFixationStop.porX, elFixationStop.porY);
      var onFixationEndSample = this.OnFixationEndSample;
      if (onFixationEndSample == null)
        return;
      onFixationEndSample(fixationEnd);
    }

    private void onEyeImage(IntPtr eyeImage)
    {
      Bitmap eyeImage1 = new Bitmap(300, 90, PixelFormat.Format24bppRgb);
      try
      {
        BitmapData bitmapdata = eyeImage1.LockBits(new Rectangle(Point.Empty, eyeImage1.Size), ImageLockMode.ReadWrite, eyeImage1.PixelFormat);
        try
        {
          DELCLib.CopyMemory(bitmapdata.Scan0, eyeImage, 81000U);
        }
        finally
        {
          eyeImage1.UnlockBits(bitmapdata);
        }
        DELCsApi.ELEyeImage onEyeImage = this.OnEyeImage;
        if (onEyeImage == null)
          return;
        onEyeImage(eyeImage1);
      }
      catch (Exception ex)
      {
        eyeImage1.Dispose();
      }
    }

    public void destroy()
    {
      if (!this.m_inited)
        return;
      this.m_inited = false;
      DELCLib.registerDeviceEventCallback((MulticastDelegate) null);
      DELCLib.registerGazeSampleCallback((MulticastDelegate) null);
      DELCLib.destroyApi();
    }

    public void connect()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot connect: module is not initialized or already destroyed");
      switch (DELCLib.connect())
      {
        case 0:
          break;
        case 1:
          throw new ELException(ELException.ErrorType.NOT_INITED, "cannot connect: module is not initialized or already destroyed");
        case 2:
          throw new ELException(ELException.ErrorType.VERSION_MISMATCH, "cannot connect: version mismatch - please update the EyeLogic Server");
        case 3:
          throw new ELException(ELException.ErrorType.CONNECTION_FAILED, "cannot connect: server not responding - is the server running?");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "cannot connect: error cause unknown");
      }
    }

    public void connectRemote(DELCsApi.ServerInfo server)
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot connect remotely: module is not initialized or already destroyed");
      DELCLib.ServerInfo serverInfo = new DELCLib.ServerInfo();
      serverInfo.ip = new byte[16];
      Encoding.ASCII.GetBytes(server.ip).CopyTo((Array) serverInfo.ip, 0);
      serverInfo.port = server.port;
      switch (DELCLib.connectRemote(serverInfo))
      {
        case 0:
          break;
        case 1:
          throw new ELException(ELException.ErrorType.NOT_INITED, "cannot connect remotely: module is not initialized or already destroyed");
        case 2:
          throw new ELException(ELException.ErrorType.VERSION_MISMATCH, "cannot connect remotely: version mismatch - please update the EyeLogic Server");
        case 3:
          throw new ELException(ELException.ErrorType.CONNECTION_FAILED, "cannot connect remotely: server not responding - is the server running?");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "cannot connect remotely: error cause unknown");
      }
    }

    public DELCsApi.ServerInfo[] requestServerList(int blockingDurationMS, int maxNumServer)
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot connect remotely: module is not initialized or already destroyed");
      DELCLib.ServerInfo[] serverList = new DELCLib.ServerInfo[maxNumServer];
      int length = DELCLib.requestServerList(blockingDurationMS, serverList, maxNumServer);
      if (length == 0)
        return (DELCsApi.ServerInfo[]) null;
      DELCsApi.ServerInfo[] serverInfoArray = new DELCsApi.ServerInfo[length];
      for (int index = 0; index < length; ++index)
      {
        serverInfoArray[index] = new DELCsApi.ServerInfo();
        serverInfoArray[index].ip = DELCsApi.decodeBytesToString(serverList[index].ip, Encoding.ASCII);
        serverInfoArray[index].port = serverList[index].port;
      }
      return serverInfoArray;
    }

    public void disconnect()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot disconnect: module is not initialized or already destroyed");
      DELCLib.disconnect();
    }

    public bool isConnected() => this.m_inited && DELCLib.isConnected();

    public DELCsApi.ScreenConfig getActiveScreen()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot get active screen: module is not initialized or already destroyed");
      DELCLib.ScreenConfig screenConfig = new DELCLib.ScreenConfig();
      DELCLib.getActiveScreen(ref screenConfig);
      return new DELCsApi.ScreenConfig()
      {
        localMachine = screenConfig.localMachine,
        id = DELCsApi.decodeBytesToString(screenConfig.id, Encoding.ASCII),
        name = DELCsApi.decodeBytesToString(screenConfig.name, Encoding.ASCII),
        resolutionX = screenConfig.resolutionX,
        resolutionY = screenConfig.resolutionY,
        physicalSizeX_mm = screenConfig.physicalSizeX_mm,
        physicalSizeY_mm = screenConfig.physicalSizeY_mm
      };
    }

    public DELCsApi.ScreenConfig[] getAvailableScreens()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot get available screens: module is not initialized or already destroyed");
      DELCLib.ScreenConfig[] screenConfig = new DELCLib.ScreenConfig[16];
      int availableScreens1 = DELCLib.getAvailableScreens(screenConfig, 16);
      if (availableScreens1 == 0)
        return (DELCsApi.ScreenConfig[]) null;
      DELCsApi.ScreenConfig[] availableScreens2 = new DELCsApi.ScreenConfig[availableScreens1];
      for (int index = 0; index < availableScreens1; ++index)
      {
        availableScreens2[index] = new DELCsApi.ScreenConfig();
        availableScreens2[index].localMachine = screenConfig[index].localMachine;
        availableScreens2[index].id = DELCsApi.decodeBytesToString(screenConfig[index].id, Encoding.ASCII);
        availableScreens2[index].name = DELCsApi.decodeBytesToString(screenConfig[index].name, Encoding.ASCII);
        availableScreens2[index].resolutionX = screenConfig[index].resolutionX;
        availableScreens2[index].resolutionY = screenConfig[index].resolutionY;
        availableScreens2[index].physicalSizeX_mm = screenConfig[index].physicalSizeX_mm;
        availableScreens2[index].physicalSizeY_mm = screenConfig[index].physicalSizeY_mm;
      }
      return availableScreens2;
    }

    public void setActiveScreen(string screenID, DELCsApi.DeviceGeometry deviceGeometry)
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot set active screen: module is not initialized or already destroyed");
      switch (DELCLib.setActiveScreen(screenID, new DELCLib.DeviceGeometry()
      {
        mmBelowScreen = deviceGeometry.mmBelowScreen,
        mmTrackerInFrontOfScreen = deviceGeometry.mmTrackerInFrontOfScreen
      }))
      {
        case 0:
          break;
        case 1:
          throw new ELException(ELException.ErrorType.NOT_INITED, "cannot set active screen: module is not initialized or already destroyed");
        case 2:
          throw new ELException(ELException.ErrorType.SCREEN_NOT_FOUND, "cannot set active screen: screenID not found");
        case 3:
          throw new ELException(ELException.ErrorType.SCREEN_FAILURE, "cannot set active screen: screen failure");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "cannot set active screen: error cause unknown");
      }
    }

    public DELCsApi.DeviceConfig getDeviceConfig()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot get device configuration: module is not initialized or already destroyed");
      DELCLib.DeviceConfig deviceConfig = new DELCLib.DeviceConfig();
      DELCLib.getDeviceConfig(ref deviceConfig);
      if (deviceConfig.deviceSerial == 0UL)
        return (DELCsApi.DeviceConfig) null;
      List<int> intList1 = new List<int>(deviceConfig.numFrameRates);
      for (int index = 0; index < deviceConfig.numFrameRates; ++index)
        intList1.Add((int) deviceConfig.frameRates[index]);
      List<int> intList2 = new List<int>(deviceConfig.numCalibrationMethods);
      for (int index = 0; index < deviceConfig.numCalibrationMethods; ++index)
        intList2.Add((int) deviceConfig.calibrationMethods[index]);
      return new DELCsApi.DeviceConfig()
      {
        deviceSerial = deviceConfig.deviceSerial,
        deviceName = DELCsApi.decodeBytesToString(deviceConfig.deviceName, Encoding.ASCII),
        brandedName = DELCsApi.decodeBytesToString(deviceConfig.brandedName, Encoding.ASCII),
        isDemoDevice = deviceConfig.isDemoDevice,
        frameRates = intList1,
        calibrationMethods = intList2
      };
    }

    public void streamEyeImages(bool enable)
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot request tracking: module is not initialized or already destroyed");
      switch (DELCLib.streamEyeImages(enable))
      {
        case 0:
          break;
        case 1:
          throw new ELException(ELException.ErrorType.NOT_CONNECTED, "cannot stream eye images: not connected to EyeLogic Server");
        case 2:
          throw new ELException(ELException.ErrorType.DEVICE_MISSING, "cannot stream eye images: feature is not supported for remote connections");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "cannot stream eye images");
      }
    }

    public void requestTracking(int frameRateModeInd)
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot request tracking: module is not initialized or already destroyed");
      switch (DELCLib.requestTracking(frameRateModeInd))
      {
        case 0:
          break;
        case 1:
          throw new ELException(ELException.ErrorType.NOT_CONNECTED, "cannot request tracking: not connected to EyeLogic Server");
        case 2:
          throw new ELException(ELException.ErrorType.DEVICE_MISSING, "cannot request tracking: no eye tracking device attached");
        case 3:
          throw new ELException(ELException.ErrorType.INVALID_FRAMERATE_MODE, "cannot request tracking: invalid framerate mode");
        case 4:
          throw new ELException(ELException.ErrorType.ALREADY_TRACKING, "cannot request tracking: device is already tracking at a different framerate");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "cannot request tracking");
      }
    }

    public void unrequestTracking()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot unrequest tracking: module is not initialized or already destroyed");
      DELCLib.unrequestTracking();
    }

    public void calibrate(int calibrationModeInd)
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot calibrate: module is not initialized or already destroyed");
      switch (DELCLib.calibrate(calibrationModeInd))
      {
        case 0:
          break;
        case 1:
          throw new ELException(ELException.ErrorType.NOT_CONNECTED, "cannot calibrate: not connected to EyeLogic Server");
        case 2:
          throw new ELException(ELException.ErrorType.NOT_TRACKING, "cannot calibrate: not tracking");
        case 3:
          throw new ELException(ELException.ErrorType.INVALID_CALIBRATION_MODE, "cannot calibrate: invalid calibration mode");
        case 4:
          throw new ELException(ELException.ErrorType.ALREADY_CALIBRATING_OR_VALIDATING, "cannot calibrate: already calibrating or validating");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "calibration not successful or aborted by user");
      }
    }

    private void abortCalibValidation()
    {
      if (!this.m_inited)
        return;
      DELCLib.abortCalibValidation();
    }

    public DELCsApi.ValidationResult validate()
    {
      if (!this.m_inited)
        throw new ELException(ELException.ErrorType.NOT_INITED, "cannot calibrate: module is not initialized or already destroyed");
      DELCLib.ValidationResult validationsData = new DELCLib.ValidationResult();
      switch (DELCLib.validate(ref validationsData))
      {
        case 0:
          List<DELCsApi.ValidationPointResult> validationPointResultList = new List<DELCsApi.ValidationPointResult>();
          for (int index = 0; index < 4; ++index)
            validationPointResultList.Add(new DELCsApi.ValidationPointResult()
            {
              validationPointPxX = validationsData.pointsData[index].validationPointPxX,
              validationPointPxY = validationsData.pointsData[index].validationPointPxY,
              meanDeviationLeftPx = validationsData.pointsData[index].meanDeviationLeftPx,
              meanDeviationLeftDeg = validationsData.pointsData[index].meanDeviationLeftDeg,
              meanDeviationRightPx = validationsData.pointsData[index].meanDeviationRightPx,
              meanDeviationRightDeg = validationsData.pointsData[index].meanDeviationRightDeg
            });
          return new DELCsApi.ValidationResult()
          {
            pointsData = validationPointResultList
          };
        case 1:
          throw new ELException(ELException.ErrorType.NOT_CONNECTED, "cannot validate: not connected to EyeLogic Server");
        case 2:
          throw new ELException(ELException.ErrorType.NOT_TRACKING, "cannot validate: not tracking");
        case 3:
          throw new ELException(ELException.ErrorType.NOT_CALIBRATED, "cannot validate: device must be calibrated");
        case 4:
          throw new ELException(ELException.ErrorType.ALREADY_CALIBRATING_OR_VALIDATING, "cannot validate: already calibrating or validating");
        default:
          throw new ELException(ELException.ErrorType.UNKNOWN_ERROR, "calibration not successful or aborted by user");
      }
    }

    public delegate void ELDeviceEvent(DeviceEventType id);

    public delegate void ELGazeSample(GazeSample sample);
    public delegate void ELFixationStartSample(FixationStartSample sample);
    public delegate void ELFixationEndSample(FixationEndSample sample);

    public delegate void ELEyeImage(Bitmap eyeImage);

    private delegate void DeviceEventCallback(int e);

    private delegate void GazeSampleCallback(ref DELCLib.GazeSample sample);
    private delegate void FixationStartCallback(ref DELCLib.ELFixationStart sample);
    private delegate void FixationEndCallback(ref DELCLib.ELFixationStop sample);

    private delegate void EyeImageCallback(IntPtr eyeImage);

    public class ServerInfo
    {
      public string ip;
      public ushort port;
    }

    public class ScreenConfig
    {
      public bool localMachine;
      public string id;
      public string name;
      public int resolutionX;
      public int resolutionY;
      public double physicalSizeX_mm;
      public double physicalSizeY_mm;
    }

    public class DeviceGeometry
    {
      public double mmBelowScreen;
      public double mmTrackerInFrontOfScreen;
    }

    public class DeviceConfig
    {
      public ulong deviceSerial;
      public string deviceName;
      public string brandedName;
      public bool isDemoDevice;
      public List<int> frameRates;
      public List<int> calibrationMethods;

      public string formatDeviceSerial()
      {
        return string.Format("{0:X2}-{1:X2}-{2:X2}-{3:X2}", (object) (ulong) ((long) (this.deviceSerial >> 24) & (long) byte.MaxValue), (object) (ulong) ((long) (this.deviceSerial >> 16) & (long) byte.MaxValue), (object) (ulong) ((long) (this.deviceSerial >> 8) & (long) byte.MaxValue), (object) (ulong) ((long) this.deviceSerial & (long) byte.MaxValue));
      }
    }

    public class ValidationPointResult
    {
      public double validationPointPxX;
      public double validationPointPxY;
      public double meanDeviationLeftPx;
      public double meanDeviationLeftDeg;
      public double meanDeviationRightPx;
      public double meanDeviationRightDeg;
    }

    public class ValidationResult
    {
      public List<DELCsApi.ValidationPointResult> pointsData;
    }
  }
}
