This is supposed to be a tool to see short-living processes. 
I've been having some trouble with a process that runs for like a second, and I could never catch it in ProcessExplorer - so maybe this would do the trick.

It's supposed to monitor for new processes using windows ETW traces. This, sadly, makes it so that you need to run the program as administrator.
Currently correctly indentifying processes that live shorter then some interval, and correctly shows the DLLs that they've loaded while they were alive.
Expanding as I go, so currently it's very much minimal.
