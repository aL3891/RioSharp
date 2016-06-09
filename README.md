# RioSharp
A .net wrapper around the registered io winsock extensions

This was inspired (and based on) by the work Ben Adams did over at the asp.net 5 benchmarking repo allthough significant changes have now been made. I wanted to create something similar to the .net socket classes while still retaining the perf of rio.

## Features
Tcp and udp support
Listening for incomming connections
Make outgoing connections
Implementation of standard .net streams for rio sockets
Low level api for directly using rio memory segments

The main objective with this project for me was perf, as opposed to security or safety of use. This project i also something i work on in my spare time.

## Usage
Rio and rosharp is based on the concept of pools of resources. Rio sharp requires users to specify upfront how many slots of memory should be used and how big they should be, as well as how many concurrent connections to accept. This is because Riosharp creates sockets in advance and reuses them using the AcceptEx windows functions. In order to listen for 10 concurrent incomming connections where memory is read/written in 256 byte chunks the following code would be used.

    var sendPool = new RioFixedBufferPool(10 , 256);
    var receivePool = new RioFixedBufferPool(10 , 256 );
    var listener = new RioTcpListener(sendPool, receivePool, 10);
    listener.OnAccepted = new Action<RioSocket>(s => ThreadPool.QueueUserWorkItem(o => Serve(o), s));
    listener.Listen(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 5000), 10);
    
The Serve() method here is responsible for completley processing the connection and then closing it witch will return in to the pool of incomming sockets. The memory size of 256 does not mean that is the maximum data that can be read or written, its only the batch size for data written and the amount of data that can be read at once. These memory blocks will be allocated upfront however so its up to the application to optimize this value for memory consumtion and performance. Ideally the size of each segment should represent the size of the data beeing sent/recived.

Creating outgoing connections is done in a similar fashion:

    var clientPool = new RioTcpClientPool(sendPool, receivePool, 10);
    RioSocket connection = await clientPool.Connect(uri);
    var stream = new RioStream(connection);
    
