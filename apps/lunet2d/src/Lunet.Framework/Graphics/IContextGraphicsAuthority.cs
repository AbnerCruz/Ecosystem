namespace Lunet.Graphics;

// The Android backend provides its GL-thread boundary; CPU and headless backends
// have no additional thread affinity. This is not a GPU sandbox.
internal interface IContextGraphicsAuthority
{
    bool ContextGraphicsAllowed { get; }
}
