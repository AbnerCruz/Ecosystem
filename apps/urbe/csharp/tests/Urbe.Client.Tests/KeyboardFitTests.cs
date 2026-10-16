using Urbe.Client;
using Xunit;

namespace Urbe.Client.Tests;

/// <summary>The phone's keyboard (16/10/2026): the app ends where the keyboard starts, never above it.</summary>
public sealed class KeyboardFitTests
{
    [Fact]
    public void AWindowNotResizedForTheKeyboardGivesItsHeightBack() =>
        Assert.Equal(700, MainView.KeyboardCover(2000, 1300));

    [Fact]
    public void AWindowAlreadyResizedForTheKeyboardIsNotPushedUpAgain()
    {
        // before, the app subtracted the keyboard's height a second time and ended half a screen above it
        Assert.Equal(0, MainView.KeyboardCover(1300, 1300));
        Assert.Equal(0, MainView.KeyboardCover(1250, 1300));
    }

    [Fact]
    public void AClosedKeyboardCoversNothing() =>
        Assert.Equal(0, MainView.KeyboardCover(2000, double.NaN));
}
