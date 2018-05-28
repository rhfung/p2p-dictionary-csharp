import time
import argparse

import clr
clr.AddReferenceToFileAndPath("P2PDictionary")
from com.rhfung.P2PDictionary import P2PDictionary
from com.rhfung.P2PDictionary.Peers import NoDiscovery


def main():
    parser = argparse.ArgumentParser(description="Create P2PDictionary daemon")
    # constructor arguments
    parser.add_argument("--description", "-m", help="Description for the server", default="")
    parser.add_argument("--port", "-p", help="Bind to port", type=int, default=8765)
    parser.add_argument("--namespace", "-ns", help="Namespace for the server", required=True)
    parser.add_argument("--timespan", "-t", default=1500, type=int, help="Timespan for searching in milliseconds")
    parser.add_argument("--discovery", "-d", default="none", choices=["none"], help="")
    # subscriptions
    parser.add_argument("--pattern", nargs="*", help="Monitors a specific pattern using wildcard (*), single character (?), and number (#) placeholders; default to *")
    parser.add_argument("--nopattern", action="store_true", help="Monitors no patterns")
    parser.add_argument("--debug", action="store_true", help="")
    parser.add_argument("--fulldebug", action="store_true", help="")
    parser.add_argument("--node", nargs="*", help="Provide a client to monitor", metavar="HOST:PORT")
    args = vars(parser.parse_args())

    if args.get('help', False):
        parser.print_help()
        return

    p2pd = P2PDictionary(
        description=args["description"], 
        port=args["port"], 
        ns=args["namespace"],
        searchForClients=args["timespan"],
        peerDiscovery=NoDiscovery(),
    )

    if args.get("pattern"):
        for single_pattern in args["pattern"]:
            p2pd.AddSubscription(single_pattern)
    elif not args.get("nopattern"):
        p2pd.AddSubscription("*")

    if args.get("fulldebug"):
        p2pd.SetDebugBuffer(StreamWriter(Console.OpenStandardOutput()), 0, True)
    elif args.get("debug"):
        p2pd.SetDebugBuffer(StreamWriter(Console.OpenStandardOutput()), 1, True)

    # todo: nodes need to be attached

    print "Server starting"
    while True:
        try:
            time.sleep(100)
        except:
            break

    print "Server ending"
    

if __name__ == "__main__":
    main()    
