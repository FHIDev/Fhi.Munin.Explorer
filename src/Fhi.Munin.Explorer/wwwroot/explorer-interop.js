// The package's one browser module, imported by ExplorerInterop and by nothing else.
//
// Importing it must stay free of side effects — no listeners, no DOM writes — because every
// caller carries on without it and a module that changed the page on import would not be optional.
//
// It exports nothing yet: this is the seam a later enhancement puts itself in. The first export
// must be named in ExplorerInterop.cs, which ExplorerInteropTest holds it to — a C# identifier
// that no longer matches one here is a permanent null with nothing in any host's log.
