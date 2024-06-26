import asyncio

from websocketserver import WebSocketServer
from tracker import OpenGazeTracker

async def data_callback(websocket_server: WebSocketServer.WebSocketServer, data: str):
    await websocket_server.send_data(data)

async def main(tracker_type: str):
    
    websocket_server = WebSocketServer.WebSocketServer('localhost', 13892)
    print('Starting bridge...')
    await websocket_server.start_server()

    data_callback_partial = lambda data: data_callback(websocket_server, data)

    if tracker_type == 'opengaze':
        tracker = OpenGazeTracker.OpenGazeTracker('localhost', 4242, data_callback_partial)
        await tracker.connect()


if __name__ == '__main__':
    asyncio.run(main('opengaze'))

