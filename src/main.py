import asyncio

from websocketserver import WebSocketServer
from tracker import OpenGazeTracker

tracker = None

async def data_callback(websocket_server: WebSocketServer.WebSocketServer, data: str):
    """
    Callback function to send data to the client.
    :param websocket_server: The WebSocket server object.
    :param data: The data to send to the client. Stringified JSON.
    """
    await websocket_server.send_data(data)

async def received_data_callback(data: dict, websocket_server: WebSocketServer.WebSocketServer):
    """
    Callback function to receive data from the client.
    :param data: The data received from the client.
    """
    print(f'Received data from client: {data}')   
    if 'type' in data:
        if data['type'] == 'config':
            await handle_received_config_data(data, websocket_server)

async def handle_received_config_data(data: dict, websocket_server: WebSocketServer.WebSocketServer):
    """
    Handle received configuration data from the client.
    :param data: The configuration data received from the client.
    """
    print(f'Received configuration data from client: {data}')
    global tracker
    if tracker is not None:
        tracker.disconnect()

    data_callback_partial = lambda data: data_callback(websocket_server, data)    
    tracker = OpenGazeTracker.OpenGazeTracker(True, 'localhost', 4242, data_callback_partial)
    await asyncio.create_task(tracker.connect()) # the tracker should send a confirmation message to the client when it is connected
    

async def main():
    """    
    websocket_server = WebSocketServer.WebSocketServer('localhost', 13892)
    print('Starting bridge...')
    await websocket_server.start_server()
    """
    websocket_server = WebSocketServer.WebSocketServer('localhost', 13892, received_data_callback)
    print('Starting bridge...')
    await websocket_server.start_server()

    try:
        await asyncio.Future()  # Run forever
    except KeyboardInterrupt:
        await websocket_server.close()

    """
    data_callback_partial = lambda data: data_callback(websocket_server, data)


    tracker = OpenGazeTracker.OpenGazeTracker(True, 'localhost', 4242, data_callback_partial)
    await tracker.connect()
    """


if __name__ == '__main__':
    asyncio.run(main())

