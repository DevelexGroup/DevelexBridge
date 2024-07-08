# -----------------------------------------------------------------------
# Copyright (C) 2019-2024, EyeLogic GmbH
#
# Permission is hereby granted, free of charge, to any person or
# organization obtaining a copy of the software and accompanying
# documentation covered by this license (the "Software") to use,
# reproduce, display, distribute, execute, and transmit the Software,
# and to prepare derivative works of the Software, and to permit
# third-parties to whom the Software is furnished to do so.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
# EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
# MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE AND
# NON-INFRINGEMENT. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR ANYONE
# DISTRIBUTING THE SOFTWARE BE LIABLE FOR ANY DAMAGES OR OTHER
# LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
# OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.
# -----------------------------------------------------------------------

## @package ELApi
# This module contains the python prototype declaration for all functions which
# are neccessary to control the EyeLogic software from an API client.

from ctypes import *
from ctypes import wintypes
from enum import Enum
import os
import sys


## contains all information about the state of the eyes at a specific time
#
# Available members:
# * <B>timestampMicroSec</B>: timepoint when data was acquired in microseconds after EPOCH
# * <B>index</B>: increasing GazeSample index
# * <B>porRawX</B>: X coordinate of binocular point of regard on the stimulus plane, check porRawX != InvalidValue before using it.
# * <B>porRawY</B>: Y coordinate of binocular point of regard on the stimulus plane, check porRawX != InvalidValue also before using porRawY.
# * <B>porFilteredX</B>: X coordinate of filtered binocular point of regard on the stimulus plane, check porFilteredX != InvalidValue before using it.
# * <B>porFilteredY</B>: Y coordinate of filtered binocular point of regard on the stimulus plane, also check porFilteredX != InvalidValue before using porFilteredY.
# * <B>porLeftX</B>: X coordinate of monocular point of regard of the left eye, check porLeftX != InvalidValue before using it.
# * <B>porLeftY</B>: Y coordinate of monocular point of regard of the left eye, also check porLeftX != InvalidValue before using porLeftY.
# * <B>eyePositionLeftX</B>: position of the left eye in device coordinates, unit is mm More...
# * <B>eyePositionLeftY</B>: position of the left eye in device coordinates, unit is mm More...
# * <B>eyePositionLeftZ</B>: position of the left eye in device coordinates, unit is mm More...
# * <B>pupilRadiusLeft</B>: radius of the left pupil in mm
# * <B>porRightX</B>: X coordinate of monocular point of regard of the right eye, check porRightX != InvalidValue before using it.
# * <B>porRightY</B>: Y coordinate of monocular point of regard of the right eye, also check porRightX != InvalidValue before using porRightY.
# * <B>eyePositionRightX</B>: position of the right eye in device coordinates, unit is mm: More...
# * <B>eyePositionRightY</B>: position of the right eye in device coordinates, unit is mm: More...
# * <B>eyePositionRightZ</B>: position of the right eye in device coordinates, unit is mm: More...
# * <B>pupilRadiusRight</B>: radius of the right pupil in mm
class ELGazeSample(Structure):
    _fields_ = [
        ("timestampMicroSec", c_int64),
        ("index", c_int32),
        ("porRawX", c_double),
        ("porRawY", c_double),
        ("porFilteredX", c_double),
        ("porFilteredY", c_double),
        ("porLeftX", c_double),
        ("porLeftY", c_double),
        ("eyePositionLeftX", c_double),
        ("eyePositionLeftY", c_double),
        ("eyePositionLeftZ", c_double),
        ("pupilRadiusLeft", c_double),
        ("porRightX", c_double),
        ("porRightY", c_double),
        ("eyePositionRightX", c_double),
        ("eyePositionRightY", c_double),
        ("eyePositionRightZ", c_double),
        ("pupilRadiusRight", c_double),
    ]


## Events coming from the eye tracker
class ELEvent(Enum):
    ## screen or resolution has changed
    SCREEN_CHANGED = 0
    ## connection to server closed
    CONNECTION_CLOSED = 1
    ## a new eye tracker has connected
    DEVICE_CONNECTED = 2
    ## the actual eye tracker has disconnected
    DEVICE_DISCONNECTED = 3
    ## tracking stopped
    TRACKING_STOPPED = 4


## contains an eye image at a specific time
class ELEyeImage(Structure):
    _fields_ = (
        [  ## pixel data of the image, width = 300, height = 90, 3 bytes per pixel RGB
            ("data", c_uint8 * 81000)
        ]
    )


## callback function type, new gaze samples
GazeSampleCallback = CFUNCTYPE(None, POINTER(ELGazeSample))
## callback function type, event occurred
EventCallback = CFUNCTYPE(None, c_int32)
## callback function type, new eye image
EyeImageCallback = CFUNCTYPE(None, POINTER(ELEyeImage))

if sys.maxsize > 2**32:
    libname = os.path.join("x64", "ELCApi")
else:
    libname = os.path.join("x86", "ELCApi32")
baseDir = os.path.dirname(os.path.abspath(__file__))
libnameGlobal = os.path.join(baseDir, libname + ".dll")
if not os.path.isfile(libnameGlobal):
    raise Exception(
        f" Could not find EyeLogic dll in its expected location: '{libnameGlobal}' ({baseDir})"
    )
try:
    kernel32 = WinDLL("kernel32", use_last_error=True)

    def check_bool(result, func, args):
        if not result:
            raise WinError(get_last_error())
        return args

    kernel32.LoadLibraryExW.errcheck = check_bool
    kernel32.LoadLibraryExW.restype = wintypes.HMODULE
    kernel32.LoadLibraryExW.argtypes = (
        wintypes.LPCWSTR,
        wintypes.HANDLE,
        wintypes.DWORD,
    )
    # 0x00000008 = LOAD_WITH_ALTERED_SEARCH_PATH
    c_libH = kernel32.LoadLibraryExW(libnameGlobal, None, 0x00000008)
    c_lib = WinDLL(libname, handle=c_libH)
except:
    raise Exception("WARNING: Failed to load '{}'".format(libnameGlobal))

## marker for an invalid double value
ELInvalidValue = c_double.in_dll(c_lib, "ELCInvalidValue").value


## main class for communication with the EyeLogic server
class ELApi:

    ## constructor
    #
    # @param   clientName       string identifier of the client (shown in the server tool
    #                           window), may be null
    def __init__(self, clientName: str):

        global c_lib
        clientNameUtf8 = clientName.encode("utf-8")
        c_lib.elInitApi.argtypes = [c_char_p]
        c_lib.elInitApi.restype = c_int
        c_lib.elInitApi(c_char_p(clientNameUtf8))

    ## destructor
    def __del__(self):
        c_lib.elDestroyApi()

    ## registers sample callback listener
    # @param   sampleCallback   this callback function is called on new gaze samples, may be null
    def registerGazeSampleCallback(self, sampleCallback: GazeSampleCallback):
        c_lib.elRegisterGazeSampleCallback.argtypes = [GazeSampleCallback]
        c_lib.elRegisterGazeSampleCallback.restype = None
        if sampleCallback is None:
            c_lib.elRegisterGazeSampleCallback(cast(None, GazeSampleCallback))
        else:
            c_lib.elRegisterGazeSampleCallback(sampleCallback)

    ## registers eye image callback listener
    # @param   eyeImageCallback   this callback function is called on new eye images, may be null
    def registerEyeImageCallback(self, eyeImageCallback: EyeImageCallback):
        c_lib.elRegisterEyeImageCallback.argtypes = [EyeImageCallback]
        c_lib.elRegisterEyeImageCallback.restype = None
        if eyeImageCallback is None:
            c_lib.elRegisterEyeImageCallback(cast(None, EyeImageCallback))
        else:
            c_lib.elRegisterEyeImageCallback(eyeImageCallback)

    ## registers event callback listener
    # @param   eventCallback   this callback function is called on eye tracking events, may be null
    def registerEventCallback(self, eventCallback: EventCallback):
        c_lib.elRegisterEventCallback.argtypes = [EventCallback]
        c_lib.elRegisterEventCallback.restype = None
        if eventCallback is None:
            c_lib.elRegisterEventCallback(cast(None, EventCallback))
        else:
            c_lib.elRegisterEventCallback(eventCallback)

    ## return values of connect( )
    class ReturnConnect(Enum):
        ## connection successully established
        SUCCESS = 0
        ## connection failed: library needs to be initialized first (constructor call missing)
        NOT_INITED = 1
        ## connection failed: API is build on a newer version than the server.
        # Update the EyeLogicServer to the newest version.
        VERSION_MISMATCH = 2
        ## connection failed: the server can not be found or is not responding
        TIMEOUT = 3

    ## initialize connection to the server (method is blocking until connection
    # established). The connection is only established for a local server (running on this
    # machine). For connections to a remote server, @see connectRemote( ).
    #
    # @return success state
    def connect(self) -> ReturnConnect:
        global c_lib
        c_lib.elConnect.argtypes = []
        c_lib.elConnect.restype = c_int32
        return ELApi.ReturnConnect(c_lib.elConnect())

    ## connection information for an EyeLogic server
    class ServerInfo:
        ## constructor
        def __init__(self):
            ## IP address of server as 0-terminated string
            self.ip = ""
            ## port of server
            self.port = 0

    ## initialize connection to a remote server (method is blocking until connection
    # established)
    #
    # @param   server  Server to connect to
    #
    # @return success state
    #
    # @see acquireServerList( ) to obtain IP address and port of a remote server
    def connectRemote(self, server: ServerInfo) -> ReturnConnect:
        global c_lib

        class ServerInfoInternal(Structure):
            _fields_ = [
                ("ip", c_uint8 * 16),
                ("port", c_int16),
            ]

        serverInternal = ServerInfoInternal()
        byte_arr = server.ip.encode("ascii")
        serverInternal.ip = (c_uint8 * 16)(*(byte_arr))
        serverInternal.port = server.port

        c_lib.elConnectRemote.argtypes = [ServerInfoInternal]
        c_lib.elConnectRemote.restype = c_int32
        return ELApi.ReturnConnect(c_lib.elConnectRemote(serverInternal))

    ## Ping all running EyeLogic servers in the local network and wait some time for their
    # response.
    #
    # @param   blockingDurationMS  waiting duration in milliseconds. Method returns after this
    #                              time, or if 'serverListLength' many servers responded.
    # @param   maxNumServer        maximum number of server to be waited for
    # @return                      List of responding EyeLogic servers
    def requestServerList(
        self, blockingDurationMS: c_int32, maxNumServer: c_int32
    ) -> [ServerInfo]:
        global c_lib

        class ServerInfoInternal(Structure):
            _fields_ = [
                ("ip", c_uint8 * 16),
                ("port", c_int16),
            ]

        ServerList = ServerInfoInternal * maxNumServer
        serverList = ServerList()
        c_lib.elConnectRemote.argtypes = [c_int32, POINTER(ServerInfoInternal), c_int32]
        c_lib.elConnectRemote.restype = c_int32
        num = c_lib.elRequestServerList(blockingDurationMS, serverList, maxNumServer)
        result = []
        for i in range(num):
            serverInfo = ELApi.ServerInfo()
            serverInfo.ip = bytes(serverList[i].ip).decode("ascii")
            serverInfo.port = int(serverList[i].port)
            result.append(serverInfo)
        return result

    ## closes connection to the server
    def disconnect(self):
        global c_lib
        c_lib.elDisconnect.argtypes = []
        c_lib.elDisconnect.restype = None
        c_lib.elDisconnect()

    ## whether a connection to the server is established
    def isConnected(self) -> bool:
        global c_lib
        c_lib.elDisconnect.argtypes = []
        c_lib.elDisconnect.restype = c_bool
        return c_lib.elIsConnected()

    ## configuration of the stimulus screen
    class ScreenConfig:
        ## constructor
        def __init__(self):
            ## whether this screen is connected to the local PC
            self.localMachine = False
            ## ID of the screen
            self.id = ""
            ## Name of the screen
            self.name = ""
            ## screen X resolution [px]
            self.resolutionX = 0
            ## screen Y resolution [px]
            self.resolutionY = 0
            ## horizontal physical dimension of the screen [mm]
            self.physicalSizeX_mm = 0.0
            ## vertical physical dimension of the screen [mm]
            self.physicalSizeY_mm = 0.0

    ## get stimulus screen configuration
    # @return    screen configuration
    def getActiveScreen(self) -> ScreenConfig:
        global c_lib

        class ScreenConfigInternal(Structure):
            _fields_ = [
                ("localMachine", c_bool),
                ("id", c_char * 32),
                ("name", c_char * 32),
                ("resolutionX", c_int32),
                ("resolutionY", c_int32),
                ("physicalSizeX_mm", c_double),
                ("physicalSizeY_mm", c_double),
            ]

        c_lib.elGetActiveScreen.argtypes = [POINTER(ScreenConfigInternal)]
        c_lib.elGetActiveScreen.restype = None
        screenConfigInternal = ScreenConfigInternal()
        c_lib.elGetActiveScreen(pointer(screenConfigInternal))

        screenConfig = ELApi.ScreenConfig()
        screenConfig.localMachine = bool(screenConfigInternal.localMachine)
        screenConfig.id = bytes(screenConfigInternal.id).decode("ascii")
        screenConfig.name = bytes(screenConfigInternal.name).decode("ascii")
        screenConfig.resolutionX = int(screenConfigInternal.resolutionX)
        screenConfig.resolutionY = int(screenConfigInternal.resolutionY)
        screenConfig.physicalSizeY_mm = c_double(screenConfigInternal.physicalSizeY_mm)
        screenConfig.physicalSizeX_mm = c_double(screenConfigInternal.physicalSizeX_mm)

        return screenConfig

    ## Get a list of screens connected to the local machine. If there are more screens than
    # 'numScreenConfigs' found, then only the first 'numScreenConfigs' ones are filled.
    #
    # @return  list of screen configurations
    def getAvailableScreens(self) -> [ScreenConfig]:
        global c_lib

        class ScreenConfigInternal(Structure):
            _fields_ = [
                ("localMachine", c_bool),
                ("id", c_char * 32),
                ("name", c_char * 32),
                ("resolutionX", c_int32),
                ("resolutionY", c_int32),
                ("physicalSizeX_mm", c_double),
                ("physicalSizeY_mm", c_double),
            ]

        screens = (ScreenConfigInternal * 16)()
        c_lib.elGetAvailableScreens.argtypes = [POINTER(ScreenConfigInternal), c_int32]
        c_lib.elGetAvailableScreens.restype = c_int32
        num = c_lib.elGetAvailableScreens(screens, 16)
        result = []
        for i in range(num):
            screenConfig = ELApi.ScreenConfig()
            screenConfig.localMachine = bool(screens[i].localMachine)
            screenConfig.id = bytes(screens[i].id).decode("ascii")
            screenConfig.name = bytes(screens[i].name).decode("ascii")
            screenConfig.resolutionX = int(screens[i].resolutionX)
            screenConfig.resolutionY = int(screens[i].resolutionY)
            screenConfig.physicalSizeX_mm = float(screens[i].physicalSizeX_mm)
            screenConfig.physicalSizeY_mm = float(screens[i].physicalSizeY_mm)

            result.append(screenConfig)
        return result

    ## geometric position of the device related to the active monitor
    class DeviceGeometry:
        ## constructor
        # @param    mmBelowScreen   distance of eye tracker below the bottom line of the screen [mm]
        # @param    mmTrackerInFrontOfScreen   distance of front panel of the eye tracker in front of
        #                           the screen[mm]
        def __init__(self, mmBelowScreen, mmTrackerInFrontOfScreen):
            ## distance of eye tracker below the bottom line of the screen [mm]
            self.mmBelowScreen = mmBelowScreen
            ## distance of front panel of the eye tracker in front of the screen[mm]
            self.mmTrackerInFrontOfScreen = mmTrackerInFrontOfScreen

    ## return values of setActiveScreen( )
    class ReturnSetActiveScreen(Enum):
        ## active screen was set
        SUCCESS = 0
        ## specified screen name was not found as a name of an available monitor
        NOT_FOUND = 1
        ## active screen could not be changed
        FAILURE = 2

    ## Make a screen connected to this machine to the active screen.
    #
    # Recording is from now on performed on the new active screen. Remember to perform a
    # calibration on the new screen, otherwise it remains in an uncalibrated state.
    #
    # @param   id        ID of the new active screen on _this_ machine (even works if the
    #                    connection to the server is remote).
    #                    If null, the primary screen of this machine is set as active.
    # @param   deviceGeometry  Geometry of the device which is mounted to the screen.
    # @return  success/error code
    def setActiveScreen(
        self, id: str, deviceGeometry: DeviceGeometry
    ) -> ReturnSetActiveScreen:
        global c_lib

        class DeviceGeometryInternal(Structure):
            _fields_ = [
                ("mmBelowScreen", c_double),
                ("mmTrackerInFrontOfScreen", c_double),
            ]

        deviceGeometryInternal = DeviceGeometryInternal()
        deviceGeometryInternal.mmBelowScreen = deviceGeometry.mmBelowScreen
        deviceGeometryInternal.mmTrackerInFrontOfScreen = (
            deviceGeometry.mmTrackerInFrontOfScreen
        )

        c_lib.elSetActiveScreen.argtypes = [c_char_p, DeviceGeometryInternal]
        c_lib.elSetActiveScreen.restype = c_int32
        return ELApi.ReturnSetActiveScreen(
            c_lib.elSetActiveScreen(
                c_char_p(id.encode("ascii")), deviceGeometryInternal
            )
        )

    ## configuration of the eye tracker
    class DeviceConfig:
        ## constructor
        # @param    deviceSerial    serial number of the device
        def __init__(self, deviceSerial):
            ## serial number
            self.deviceSerial = deviceSerial
            ## name of the device
            self.deviceName = ""
            ## name of the license owner
            self.brandedName = ""
            ## whether the device is for DEMO use only, not for public sale
            self.isDemoDevice = False
            ## list of supported frame rates
            self.frameRates = []
            ## list of supported calibration methods (number of shown points)
            self.calibrationMethods = []

    ## get configuration of actual eye tracker device
    # @return    device configuration
    def getDeviceConfig(self) -> DeviceConfig:
        global c_lib

        class DeviceConfigInternal(Structure):
            _fields_ = [
                ("deviceSerial", c_int64),
                ("deviceName", c_char * 32),
                ("brandedName", c_char * 64),
                ("isDemoDevice", c_bool),
                ("numFrameRates", c_int32),
                ("frameRates", c_uint8 * 16),
                ("numCalibrationMethods", c_int32),
                ("calibrationMethods", c_uint8 * 16),
            ]

        c_lib.elGetDeviceConfig.argtypes = [POINTER(DeviceConfigInternal)]
        c_lib.elGetDeviceConfig.restype = None
        deviceConfigInternal = DeviceConfigInternal()
        c_lib.elGetDeviceConfig(deviceConfigInternal)
        deviceConfig = ELApi.DeviceConfig(
            c_int64(deviceConfigInternal.deviceSerial).value
        )
        deviceConfig.deviceName = bytes(deviceConfigInternal.deviceName).decode("ascii")
        deviceConfig.brandedName = bytes(deviceConfigInternal.brandedName).decode(
            "ascii"
        )
        deviceConfig.isDemoDevice = c_bool(deviceConfigInternal.isDemoDevice).value
        for i in range(deviceConfigInternal.numFrameRates):
            deviceConfig.frameRates.append(
                c_uint8(deviceConfigInternal.frameRates[i]).value
            )
        for i in range(deviceConfigInternal.numCalibrationMethods):
            deviceConfig.calibrationMethods.append(
                c_uint8(deviceConfigInternal.calibrationMethods[i]).value
            )
        return deviceConfig

    ## return values of streamEyeImages( )
    class ReturnStreamEyeImages(Enum):
        ## setting streaming of eye images was successful
        SUCCESS = 0
        ## failed, not connected to the server
        NOT_CONNECTED = 1
        ## cannot stream eye images when connection to the server is a remote connection
        REMOTE_CONNECTION = 2
        ## failure when trying to set eye image stream
        FAILURE = 3

    ## Enabled/disables eye image stream. If enabled, eye images are received from eye image
    # listeners, @see registerEyeImageListener() and @see getNextEyeImage(). Note, that enabling
    # eye images can lead to noticable CPU load and a loss of gaze samples. Always disable it
    # before running your experiment. Eye images can not be received via remote connections.
    def streamEyeImages(self, enable: c_bool) -> ReturnStreamEyeImages:
        global c_lib
        c_lib.elStreamEyeImages.argtypes = [c_bool]
        c_lib.elStreamEyeImages.restype = c_int
        ret = ELApi.ReturnStreamEyeImages(c_lib.elStreamEyeImages(enable))
        return ret

    ## return values of getNextEvent( ), getNextGazeSample( ) and getNextEyeImage( )
    class ReturnNextData(Enum):
        ## new event or new GazeSample received
        SUCCESS = 0
        ## library needs to be initialized first
        NOT_INITED = 1
        ## timeout reached, no new event/GazeSample received
        TIMEOUT = 2
        ## connection to server closed, no new event/GazeSample received
        CONNECTION_CLOSED = 3

    ## Obtains the next unread event or blocks until a new event occurs or the given timeout is reached.
    #
    # The last incoming event is buffered internally and can be obtained by calling this method in
    # a consecutive order. If there is no new event, the method blocks until an event occurs or the
    # given timeout is reached. The method returns SUCCESS if and only if a new event is provided
    # which was not returned before. Therefore, by checking the return value, you can assure to not
    # handle any event twice.
    #
    # If you want to catch events in a loop, be careful to not wait too long between the calls to
    # this method. Otherwise, you may miss events. If you want to be 100% sure to not miss any
    # event, consider to use the ELEventCallback mechanism. @see registerEventListener
    #
    # @param   timeoutMillis   duration in milliseconds, method returns at the latest after this
    #                          time. May be 0 if the method should return immediatly.
    # @return  first: new (yet unhandled) event. second: whether an event was received (SUCCESS) or
    #          the method terminated without a new event
    def getNextEvent(self, timeoutMillis: c_int) -> (ReturnNextData, ELEvent):
        global c_lib
        c_lib.elGetNextEvent.argtypes = [POINTER(ELEvent), c_int]
        c_lib.elGetNextEvent.restype = c_int
        event = ELEvent()
        ret = ELApi.ReturnNextData(c_lib.elGetNextEvent(pointer(event), timeoutMillis))
        return (ret, event)

    ## Obtains the next unread gazeSample or blocks until a new GazeSample is received or the
    # given timeout is reached.
    #
    # The last incoming GazeSample is buffered internally and can be obtained by calling this
    # method in a consecutive order. If there is no new GazeSample, the method blocks until a
    # GazeSample arrives or the given timeout is reached. The method returns SUCCESS if and only if
    # a new GazeSample is provided which was not returned before. Therefore, by checking the return
    # value, you can assure to not handle any GazeSample twice.
    #
    # If you want to catch GazeSamples in a loop, be careful to not wait too long between the calls
    # to this method (at least once per frame). Otherwise, you may miss GazeSamples. If you want to
    # be 100% sure to not miss any GazeSample, consider to use the ELGazeSampleCallback mechanism.
    # @see registerGazeSampleListener
    #
    # @param   timeoutMillis   duration in milliseconds, method returns at the latest after this
    #                          time. May be 0 if the method should return immediatly.
    # @return  first: new (yet unhandled) GazeSample. second: whether an event was received (SUCCESS)
    #          or the method terminated without a new GazeSample
    def getNextGazeSample(self, timeoutMillis: c_int) -> (ReturnNextData, ELGazeSample):
        global c_lib
        c_lib.elGetNextGazeSample.argtypes = [POINTER(ELGazeSample), c_int]
        c_lib.elGetNextGazeSample.restype = c_int
        gazeSample = ELGazeSample()
        ret = ELApi.ReturnNextData(
            c_lib.elGetNextGazeSample(pointer(gazeSample), timeoutMillis)
        )
        return (ret, gazeSample)

    ## Obtains the next unread eye image or blocks until a new eye image is received or the
    # given timeout is reached.
    #
    # The last incoming eye image is buffered internally and can be obtained by calling this method
    # in a consecutive order. If there is no new eye image, the method blocks until an eye image is
    # received or the given timeout is reached. The method returns SUCCESS if and only if a new
    # eye image is provided which was not returned before. Therefore, by checking the return value,
    # you can assure to not handle any eye image twice.
    #
    # If you want to catch EyeImages in a loop, be careful to not wait too long between the calls
    # to this method (at least once per frame). Otherwise, you may miss EyeImages. If you want to
    # be 100% sure to not miss any EyeImages, consider to use the ELEyeImagesCallback mechanism.
    # @see registerEyeImagesListener
    #
    # @param   timeoutMillis   duration in milliseconds, method returns at the latest after this
    #                          time. May be 0 if the method should return immediatly.
    # @return  first: new (yet unhandled) EyeImages. second: whether an event was received (SUCCESS)
    def getNextEyeImage(self, timeoutMillis: c_int) -> (ReturnNextData, ELEyeImage):
        global c_lib
        c_lib.elGetNextEyeImage.argtypes = [POINTER(ELEyeImage), c_int]
        c_lib.elGetNextEyeImage.restype = c_int
        eyeImage = ELEyeImage()
        ret = ELApi.ReturnNextData(
            c_lib.elGetNextEyeImage(pointer(eyeImage), timeoutMillis)
        )
        return (ret, eyeImage)

    ## return values of requestTracking( )
    class ReturnStart(Enum):
        ## start tracking successful
        SUCCESS = 0
        ## not connected to the server
        NOT_CONNECTED = 1
        ## cannot start tracking: no device found
        DEVICE_MISSING = 2
        ## cannot start tracking: framerate mode is invalid or not supported
        INVALID_FRAMERATE_MODE = 3
        ## tracking already ongoing, but frame rate mode is different
        ALREADY_RUNNING_DIFFERENT_FRAMERATE = 4
        ## some general failure occurred
        FAILURE = 5

    ## request tracking
    #
    # If tracking is not yet ongoing, tracking is started in the device. If tracking is already
    # running (e.g. started from another client) with the same frame-rate as requested, all gaze
    # samples are reported to this client as well.
    #
    # @param   frameRateModeInd    index of the requested frame rate mode (0 .. \#frameRateModes-1)
    # @returns success state
    def requestTracking(self, frameRateModeInd: c_int) -> ReturnStart:
        global c_lib
        c_lib.elRequestTracking.argtypes = [c_int]
        c_lib.elRequestTracking.restype = c_int
        return ELApi.ReturnStart(c_lib.elRequestTracking(frameRateModeInd))

    ## unrequest tracking
    #
    # Note that the tracking device may continue if other processes still request tracking. Check
    # the EyeLogic server window to observe the actual state.
    def unrequestTracking(self):
        global c_lib
        c_lib.elUnrequestTracking.argtypes = []
        c_lib.elUnrequestTracking.restype = None
        c_lib.elUnrequestTracking()

    ## return values of calibrate( )
    class ReturnCalibrate(Enum):
        ## calibration successful
        SUCCESS = 0
        ## cannot calibrate: not connected to the server
        NOT_CONNECTED = 1
        ## cannot calibrate: no device found or tracking not started
        NOT_TRACKING = 2
        ## cannot start calibration: calibration mode is invalid or not supported
        INVALID_CALIBRATION_MODE = 3
        ## cannot start calibration: calibration or validation is already in progress
        ALREADY_BUSY = 4
        ## calibration was not successful or aborted
        FAILURE = 5

    ## perform calibration (method is blocking until calibration finished)
    #
    # @returns success state
    def calibrate(self, calibrationModeInd: c_int):
        global c_lib
        c_lib.elCalibrate.argtypes = [c_int]
        c_lib.elCalibrate.restype = c_int
        return ELApi.ReturnCalibrate(c_lib.elCalibrate(calibrationModeInd))

    ## abort a running calibration / validation
    def abortCalibValidation(self):
        global c_lib
        c_lib.abortCalibValidation()

    ## return values of validate( )
    class ReturnValidate(Enum):
        ## start validation successful
        SUCCESS = 0
        ## cannot validate: not connected to the server
        NOT_CONNECTED = 1
        ## cannot validate: no device found or tracking not started
        NOT_TRACKING = 2
        ## cannot start validation: validation mode is invalid or not supported
        NOT_CALIBRATED = 3
        ## cannot start validation: calibration or validation is already in progress
        ALREADY_BUSY = 4
        ## validation failure
        FAILURE = 5

    ## ValidationPointResult
    #
    # Holds the results of the validation ( total deviation between true point position and
    # calculated POR of the left and right eye POR in [px] and  [deg] ) of the validation point
    # at position ( validationPointPxX, validationPointPxY ) [px].
    #
    # The stimulus point position and deviation [px] are given in the 2D stimulus coordinate
    # system originating in the top left corner of the stimulus.
    #
    # The deviation [deg] corresponds to the total angular deviation between the measured gaze
    # direction from the ground truth gaze direction as determined according to the measured eye
    # position.
    #
    # Note: meanDeviation* data fields may be ELInvalidValue. The pairs meanDeviationLeftDeg/-Px
    # and meanDeviationRightDeg-/Px are always either both valid or both ELInvalidValue.
    class ValidationPointResult:
        def __init__(self):
            ## ELInvalidValue or x-coordinate of stimulus point position
            self.validationPointPxX = ELInvalidValue
            ## ELInvalidValue or y-coordinate of stimulus point position
            self.validationPointPxY = ELInvalidValue
            ## ELInvalidValue or mean deviation between left eye POR and stimulus position in [px]
            # in the stimulus plane
            self.meanDeviationLeftPx = ELInvalidValue
            ## ELInvalidValue or mean deviation of left eye gaze direction in [deg] in the 3-D
            # world system
            self.meanDeviationLeftDeg = ELInvalidValue
            ## ELInvalidValue or mean deviation between right eye POR and stimulus position in [px]
            # in the stimulus plane
            self.meanDeviationRightPx = ELInvalidValue
            ## ELInvalidValue or mean deviation of right eye gaze direction in [deg] in the 3-D
            # world system
            self.meanDeviationRightDeg = ELInvalidValue

    ## ValidationResult
    #
    # Contains one a list of ValidationPointResults - one per validation stimulus point of the
    # performed valdation.
    class ValidationResult:
        def __init__(self):
            ## Number of validation points. The following arrays will hold twice this amount in
            # valid (x, y)-tuple data points
            self.pointsData = []

    ## perform calibration (method is blocking until calibration finished) - calibration must be
    # performed prior
    #
    # @returns whether was completed successfully (SUCCESS) or error value and an instance of
    # ValidationResult. Upon SUCCESS ValidationResult.pointsData will contain each stimulus point's
    # validation data, empty list otherwise.
    def validate(self) -> (ReturnValidate, ValidationResult):
        global c_lib

        class ValidationPointResultInternal(Structure):
            _fields_ = [
                ("validationPointPxX", c_double),
                ("validationPointPxY", c_double),
                ("meanDeviationLeftPx", c_double),
                ("meanDeviationLeftDeg", c_double),
                ("meanDeviationRightPx", c_double),
                ("meanDeviationRightDeg", c_double),
            ]

        class ValidationResultInternal(Structure):
            _fields_ = [
                ("pointsData", ValidationPointResultInternal * 4),
            ]

        c_lib.elValidate.argtypes = [POINTER(ValidationResultInternal)]
        c_lib.elValidate.restype = c_int
        validationResultInternal = ValidationResultInternal()
        rval = ELApi.ReturnValidate(c_lib.elValidate(validationResultInternal))
        validationResult = ELApi.ValidationResult()
        if rval == ELApi.ReturnValidate.SUCCESS:
            for i in range(len(validationResultInternal.pointsData)):
                validationPointResult = ELApi.ValidationPointResult()
                validationPointResult.validationPointPxX = (
                    validationResultInternal.pointsData[i].validationPointPxX
                )
                validationPointResult.validationPointPxY = (
                    validationResultInternal.pointsData[i].validationPointPxY
                )
                validationPointResult.meanDeviationLeftPx = (
                    validationResultInternal.pointsData[i].meanDeviationLeftPx
                )
                validationPointResult.meanDeviationLeftDeg = (
                    validationResultInternal.pointsData[i].meanDeviationLeftDeg
                )
                validationPointResult.meanDeviationRightPx = (
                    validationResultInternal.pointsData[i].meanDeviationRightPx
                )
                validationPointResult.meanDeviationRightDeg = (
                    validationResultInternal.pointsData[i].meanDeviationRightDeg
                )
                validationResult.pointsData.append(validationPointResult)
        return (rval, validationResult)
