import asyncio

from websocketserver import WebSocketServer
from tracker import OpenGazeTracker

async def data_callback(websocket_server: WebSocketServer.WebSocketServer, data: str):
    """
    Callback function to send data to the client.
    :param websocket_server: The WebSocket server object.
    :param data: The data to send to the client. Stringified JSON.
    """
    await websocket_server.send_data(data)


async def main():
    """    
    websocket_server = WebSocketServer.WebSocketServer('localhost', 13892)
    print('Starting bridge...')
    await websocket_server.start_server()
    """
    websocket_server = WebSocketServer.WebSocketServer('localhost', 13892)
    print('Starting bridge...')
    await websocket_server.start_server()

    data_callback_partial = lambda data: data_callback(websocket_server, data)


    tracker = OpenGazeTracker.OpenGazeTracker('localhost', True, 4242, data_callback_partial)
    await tracker.connect()



if __name__ == '__main__':
    asyncio.run(main())

