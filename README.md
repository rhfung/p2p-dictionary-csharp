P2P Dictionary
==============

P2P Dictionary is a distributed key-value store for multiple nodes
on a local area network. Each node will subscribe to 
a subset of key-value pairs. Key-value pairs are replicated as necessary
between nodes to reach to another node. Similar to most 
NoSQL implementations, it does not provide an SQL interface or
guarantee ACID (atomicity, consistency, isolation, durability).

P2P dictionary will run on a local area network discovered using 
LAN discovery technologies (e.g., Apple Bonjour, Zeroconf, UDP broadcast)
or any reachable IP address in a public network. Peer links can be discovered
or connected by the client.

P2P dictionary provides a server written in 
[.NET Framework](https://github.com/rhfung/p2p-dictionary-csharp), 
[.NET Core](https://github.com/rhfung/p2p-dictionary-csharp),
and [Java JVM](https://github.com/rhfung/p2p-dictionary). 
A REST interface is provided by the P2P server for read-only access
to key-value pairs. A web interface is provided for web browser access 
to key-value pairs stored on each node. A redistributable package is provided
using Docker containers with both .NET and Java implementations.


Copyright (C) 2011-2018, Richard H Fung

License
-------

You agree to the LICENSE before using this Software.

Basic requirements
------------------

All platforms:
* Microsoft .NET Core 2.x on Linux, Mac, or Windows

For redistributable:
* Docker 17.05 or higher

For Mac:
* Visual Studio Code, or
* Visual Studio for Mac

For Windows:
* Microsoft Visual Studio 2017
* Microsoft .NET Framework 4.6.1 on Windows
* Requires Apple Bonjour Print Services for Windows:
  http://support.apple.com/kb/DL999

Getting Started
---------------

Several example projects using P2P Dictionary are in the ''examples'' directory.

Documentation
-------------

### Performance

* Fastest response time for dictionary updates is 8 ms.

### Methods

P2PDictionary methods conform to IDictionary interface. Additional methods are:

Additional dictionary methods:

* `Clear()` removes all dictionary entries owned by this dictionary
* `GetValue(key,msTimeout)` blocking call to read from the dictionary, waits for msTimeout, throws IndexOutOfRangeException
* `TryGetValue(key)` blocking call to read from the dictionary, returns false if cannot get the value
* `TryGetValue(key,msTimeOut)` blocking call to read from the dictionary, waits for msTimeout, returns false if cannot get the value
* `AddSubscription("pattern")` adds a subscription that matches the pattern. Pattern matching is Visual Basic patterns (* for many characters, ? for a single character)

Control methods:

* `Abort()` force close all incoming and outgoing connections
* `Close()` safely close all incoming and outgoing connections
* `ConstructNetwork()` searches for peers on the network using Apple Bonjour -- must be enabled ahead of time??
* `GetSubscriptions()` returns a list of subscriptions (subscribed patterns)
* `OpenClient()` opens a client connection to a known remote peer
* `OpenServer()` opens a server instance if not already specified on constructor
* `RemoveSubscription()` removes a previously added subscription '''not tested'''

Static methods:

* `P2PDictionary.GetFreePort(port)` returns the next open free port at or above the specified value. Throws ApplicationException if the port cannot be found.

### Properties

* `DebugBuffer` sets/gets the buffer for debug messages
* `Description` returns the friendly name for the dictionary, assigned on constructor
* `LocalEndPoint` returns the IP address of the server
* `LocalID` returns a unique ID number for the dictionary, should be unique to all peers
* `Namespace` returns the namespace provided on constructor, must be the same to all peers on the network
* `RemotePeersCount` returns the number of remotely connected peers

### Events

* `Connected` when a peer joins
* `ConnectionFailure` when a peer fails to join
* `Disconnected` when a peer departs
* `Notified` when a subscribed dictionary key is added, changed, or removed
* `SubscriptionChanged` when a subscription changes

Building
--------

Build on Linux/Mac using Docker. Example commands:

    cd <SOURCEDIR>
    docker build -t p2pd-csharp .
    docker run -p 8800:8800 -it p2pd-csharp -s test -p 8800

And open http://localhost:8800 to see the running server.

Get additional commands using (see next section for detail):

    docker run -p 8800:8800 -it p2pd-csharp --help

Command Line Interface
----------------------

The CLI `p2pd` and `p2pwin` support the following arguments:

      -m, --description    (Default: ) Description for the server
      -p, --port           (Default: 8765) Bind to port
      -s, --namespace      Required. Namespace for the server
      -t, --timespan       (Default: 1500) Search interval for clients
      -d, --discovery      Specify a backend discovery mechanism, defaults to none
      --nopattern          Monitors no patterns
      --pattern            Monitors a specific pattern using wildcard (*), single character (?), and number (#) placeholders; default to *
      -n                   Provide clients in the form host:port
      --debug
      --fulldebug

Change Log
----------

* 2.1
  * added supported for .NET Core 2.0
  * upgraded to .NET Framework 4.6.1 and Visual Studio 2017
  * added in required constructor argument for `IPeerInterface` and default discovery module `NoDiscovery`
  * added CLI for [Windows p2pwin](src/p2pwin) and [Linux p2pd](src/p2pd)
  * new discovery module [P2PDictionary.Peers.Zeroconf](src/P2PDictionary.Peers.Zeroconf) for `ZeroconfDiscovery` module that was previously enabled by default

* 2.0
  * new REST API
  * Bonjour registration
  * support for any MIME type
  * not compatible with 1.6.3
