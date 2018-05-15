P2P Dictionary
==============

P2P Dictionary is a distributed key-value store for multiple computers
on a local area network. Each computer runs a P2P server, which replicates
a subset of stored dictionary entries (key-value pairs). Each computer 
chooses a subset of keys to subscribe to. This dictionary provides an 
API written for .NET and Java applications. A REST interface is provided
by the P2P server for read-only access to dictionary entries. A local area
network is defined by Apple Bonjour's local service discovery. Similar to
other NoSQL implementations, it does not provide an SQL interface or
guarantee ACID (atomicity, consistency, isolation, durability).

Copyright (C) 2011-2018, Richard H Fung

License
-------

You agree to the LICENSE before using this Software.

Basic requirements
------------------

* Requires Apple Bonjour Print Services for Windows:
  http://support.apple.com/kb/DL999
* Microsoft Windows
* Microsoft Visual Studio 2017
* Microsoft .NET Framework 4.6.1 on Windows PC
* Microsoft .NET Core 2.x

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

Change Log
----------

* 2.1: upgraded to .NET Framework 4.6.1 and Visual Studio 2017
* 2.0: new REST API, Bonjour registration, support for any MIME type, and stability bug fixes. Not compatible with 1.6.3. Cross-platform.

