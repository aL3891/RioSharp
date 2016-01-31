# RioSharp
A .net wrapper around the registered io winsock extensions

This was inspired (and based on) by the work Ben Adams did over at the asp.net 5 benchmarking repo. I wanted to create soemthing similar to the .net socket classes while still retaining the perf of rio.

Some other goals are/where

* Exposing RIO as regular .net streams
* Strike a balance between the nature of Rio and the socket apis we're used to
* Support making outgoing http calls using rio (via HttpClient)
* Enable high performance benchmarking of tcp/udp and http servers (similar to wrk but on windows)

So far the basic tcp/http scenario works, however even that is very experimental and the code can probably be cleaned up significantly. The main objective with this project for me was perf, as opposed to security or safety of use. This project i also something i work on between putting kids to bed and passing out on the couch, so it might not be production level, lets say.

Currently my road map is something like this:

* Implement support for doing outgoing tcp calls - _Done_
* Implement simple http server and client for benchmarking - _Done_
* Implement support for udp - _Done_
* Implement socket reuse via ConnectEx, AcceptEx and DisconnectEx _Done_
* Add ctstraffic test client and server - _In progress_
* Implement httpClient support
