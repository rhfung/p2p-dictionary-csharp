P2P Dictionary can be used as a publisher-subscriber dictionary.
This project shows an example of broadcasting a webcam stream to several subscribers. 

* CameraSource: captures the webcam video
* CameraReceiver: shows the latest webcam frame
* extlib: 3rd party libraries used for webcam capture

# Camera Receiver

Receives the video stream sent from the `CameraSource`

## CameraReceiver directory

CameraReceiver is a .NET application that receives the `CameraSource` video source. 
The `CameraSource` node is discovered using Apple Bonjour.

## camera_receiver_v2.html

2nd iteration of the camera receiver on the web.

To run this, a [P2P server](https://github.com/rhfung/p2p-dictionary) should be started. 
A compatible P2P server is available at https://github.com/rhfung/p2p-dictionary

### Running the HTML file

When running `camera_receiver_v2.html`, replace `192.168.99.100` with the IP address 
of a compatible P2P server.

### Starting a compatible P2P Server

Download a compatible P2P server from https://github.com/rhfung/p2p-dictionary.
The P2P server requires Java, but it can also be started using Docker with the `start` script.

From the main directory of the project, run the following command:

	./start -ns cameratest -n <ip_address>:2011 --debug
	
where `<ip_address>` is the IP address of the computer running the `CameraSource`.

# Change Log

* January 1, 2016:
  * Switched from MemoryStream to byte array for downloading directly from a web browser.
  * Added in another camera receiver.
