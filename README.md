P2P Dictionary
==============

P2P Dictionary is a distributed key-value store for multiple computers on a local area network. Each computer runs a P2P server, which replicates a subset of stored dictionary entries (key-value pairs). Each computer chooses a subset of keys to subscribe to. This dictionary provides an API written for .NET and Java applications. A REST interface is provided by the P2P server for read-only access to dictionary entries. A local area network is defined by Apple Bonjour's local service discovery. Similar to other NoSQL implementations, it does not provide an SQL interface or guarantee ACID (atomicity, consistency, isolation, durability).

Copyright (C) 2011-2014, Richard H Fung

License
-------

You agree to the LICENSE before using this Software.

Basic requirements
------------------

* Requires Apple Bonjour Print Services for Windows:
  http://support.apple.com/kb/DL999
* Microsoft Windows
* Microsoft Visual Studio 2013
* Microsoft .NET Framework 4.0 on Windows PC
* Custom build of Mono.Zeroconf

Documentation
-------------

This library needs a lot more documentation. I have written documentation for parts of P2P Dictionary, which are available on my website.

* Overview: http://www.rhfung.com/core/Engineering/P2PDictionary
* Java library API: http://www.rhfung.com/more/p2p-dict-2.0-javadoc/
* REST protocol: http://www.rhfung.com/core/Engineering/P2PProtocolDocumentation

Getting Started
---------------

Several example projects using P2P Dictionary are in the ''examples'' directory.

Change Log
----------

* Split p2p-dictionary repository into a Java and C-Sharp version.
* 2.0.x: new REST API, Bonjour registration, support for any MIME type, and stability bug fixes. Not compatible with 1.6.3. Cross-platform.