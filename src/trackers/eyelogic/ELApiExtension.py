import trackers.eyelogic.api.ELApi as EL


def handle_connect_return(code: EL.ELApi.ReturnConnect) -> tuple[bool, str]:
    match code:
        case EL.ELApi.ReturnConnect.SUCCESS:
            return True, ""
        case EL.ELApi.ReturnConnect.NOT_INITED:
            return False, "library not initialized"
        case EL.ELApi.ReturnConnect.VERSION_MISMATCH:
            return False, "library version mismatch"
        case EL.ELApi.ReturnConnect.TIMEOUT:
            return False, "connection timeout"
        case _:
            return False, "unknown error"


def handle_start_return(code: EL.ELApi.ReturnStart) -> tuple[bool, str]:
    match code:
        case EL.ELApi.ReturnStart.SUCCESS:
            return True, ""
        case EL.ELApi.ReturnStart.NOT_CONNECTED:
            return False, "eyetracker not connected"
        case EL.ELApi.ReturnStart.DEVICE_MISSING:
            return False, "no device was found"
        case EL.ELApi.ReturnStart.INVALID_FRAMERATE_MODE:
            return False, "frame rate mode is invalid"
        case EL.ELApi.ReturnStart.ALREADY_RUNNING_DIFFERENT_FRAMERATE:
            return False, "already running with different frame rate"
        case EL.ELApi.ReturnStart.FAILURE:
            return False, "generic failure occured"
        case _:
            return False, "unknown error"


def handle_calibrate_return(code: EL.ELApi.ReturnCalibrate) -> tuple[bool, str]:
    match code:
        case EL.ELApi.ReturnCalibrate.SUCCESS:
            return True, ""
        case EL.ELApi.ReturnCalibrate.NOT_CONNECTED:
            return False, "eyetracker not connected"
        case EL.ELApi.ReturnCalibrate.NOT_TRACKING:
            return False, "no device found or tracking not started"
        case EL.ELApi.ReturnCalibrate.INVALID_CALIBRATION_MODE:
            return False, "calibration mode is invalid or not supported"
        case EL.ELApi.ReturnCalibrate.ALREADY_BUSY:
            return False, "calibration or validation is already in progress"
        case EL.ELApi.ReturnCalibrate.FAILURE:
            return False, "calibration was not successful or aborted"
        case _:
            return False, "unknown error"
