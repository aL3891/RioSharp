# RioSharp
A .net wrapper around the registered io winsock extensions

This was inspired by the work Ben Adams did over at the asp.net 5 benchmarking repo. I wanted to create soemthing similar to the .net socket classes while still retaining the perf of rio.

Some other goals are/where

* Exposing RIO as regular .net streams
* Support Udp
* Support making outgoing http calls using rio (via HttpHandlers)

So far the basic tcp/http scenario works, however even that is very experimental and the code can probably be cleaned up significantly. The main objective with this project for me was perf, as opposed to security or safety of use.
