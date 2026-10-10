# stb_truetype C# (third-party)

Vendored **source** from [StbSharp/StbTrueTypeSharp](https://github.com/StbSharp/StbTrueTypeSharp) at commit `5985efcf9ae295508cda180a15dfcf11ba18578d`. The upstream README explicitly identifies the code as **public domain** (derived from Sean Barrett's stb_truetype). No binary or font files are bundled here.

This small portable dependency is compiled into Lunet.Framework with `STBSHARP_INTERNAL`. It is **not** a public API, requires no native Android library or runtime download and keeps the Framework dependency graph free of NuGet package references. Keep upstream attribution when updating, and re-run the entire framework/Android test suite.
