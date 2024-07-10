# develex-bridge

This is a Python WebSocket bridge between remote eye-tracking devices and the develex-core JavaScript library.

## WebSocket Messages

The bridge sends and receives messages in JSON format to communicate with the develex-core library.

### Incoming Messages

The bridge listens for the following messages from the develex-core library:

1. `connect` - The develex-core library sends this message to the bridge to establish a connection. It contains configuration.
2. `start` - The develex-core library sends this message to the bridge to start emitting points data of gaze.
3. `stop` - The develex-core library sends this message to the bridge to stop emitting points data of gaze.
4. `calibrate` - The develex-core library sends this message to the bridge to start the calibration process.
5. `disconnect` - The develex-core library sends this message to the bridge to close the connection with the eye-tracking device.

#### 1. `connect`

For GazePoint eye-trackers (or any other using Open Gaze API), the `connect` message should look like this:

```json
{
  "type": "connect",
  "tracker": "opengaze",
  "keepFixations": true | false,
}
```

For SMI eye-trackers, the `connect` message should look like this:

```json
{
  "type": "connect",
  "tracker": "smi"
}
```

For EyeLogic eye-trackers, the `connect` message should look like this:

```json
{
  "type": "connect",
  "tracker": "eyelogic"
}
```

Note that additional fields are probably about to be added to the `connect` message.

#### 2. `start`

The `start` message should look like this:

```json
{
  "type": "start"
}
```

It is same for all eye-trackers.

#### 3. `stop`

The `stop` message should look like this:

```json
{
  "type": "stop"
}
```

It is same for all eye-trackers.

#### 4. `calibrate`

The `calibrate` message should look like this:

```json
{
  "type": "calibrate"
}
```

#### 5. `disconnect`

The `disconnect` message should look like this:

```json
{
  "type": "disconnect"
}
```

It is same for all eye-trackers.

### Outgoing Messages

The bridge sends the following messages to the develex-core library:

1. `connected` - The bridge sends this message to the develex-core library to confirm that the connection has been established.
2. `point` - The bridge sends this message to the develex-core library to emit gaze points data.
3. `calibrated` - The bridge sends this message to the develex-core library to confirm that the calibration process has been completed.
4. `disconnected` - The bridge sends this message to the develex-core library to confirm that the connection has been closed.
5. `error` - The bridge sends this message to the develex-core library to report an error.
6. `started` - The bridge sends this message to the develex-core library to confirm that the start was successfull.
7. `stopped` - The bridge sends this message to the develex-core library to confirm that the stop was successfull.

#### 1. `connected`

The `connected` message should look like this:

```json
{
  "type": "connected"
}
```

#### 2. `point`

The `point` message should look like this:

```json
{
  "type": "point",
  "xL": "The x-coordinate of the gaze point. Left eye.",
  "yL": "The y-coordinate of the gaze point. Left eye.",
  "validityL": "A boolean indicating whether the data is valid or not. Left eye.",
  "xR": "The x-coordinate of the gaze point. Right eye.",
  "yR": "The y-coordinate of the gaze point. Right eye.",
  "validityR": "A boolean indicating whether the data is valid or not. Right eye.",
  "timestamp": "The timestamp of the data in seconds.",
  "fixationId": "The ID of the fixation. This is only present if the data is a fixation. (!!!)",
  "fixationDuration": "The duration of the fixation in seconds. This is only present if the data is a fixation. (!!!)"
}
```

#### 3. `calibrated`

The `calibrated` message should look like this:

```json
{
  "type": "calibrated"
}
```

Note, this will in the future hold more fields containing calibration data.

#### 4. `disconnected`

The `disconnected` message should look like this:

```json
{
  "type": "disconnected"
}
```

#### 5. `error`

The `error` message convey when there is an error in the bridge. It should look like this:

```json
{
  "type": "error",
  "message": "The error message."
}
```

#### 6. `started`

```json
{
  "type": "started"
}
```

#### 7. `stopped`

```json
{
  "type": "stopped"
}
```
