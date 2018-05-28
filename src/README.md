The main source is currently split into different projects:

* P2PDictionary: base library for running the server
* P2PDictionary.Persistence: library for saving contents in XML (unsupported)
* P2PDictionary.Peers.Zeroconf: Windows library to be distributed with `Mono.Zeroconf` and `Mono.Zeroconf.Providers.Bonjour` for Apple Bonjour service discovery
* p2pd: cross-platform CLI for running a server without discovery
* p2pwin: Windows-only CLI for running a server with Bonjour discovery
