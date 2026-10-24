using Urbe.Client;
using Xunit;

namespace Urbe.Client.Tests;

public sealed class KeyboardFocusPolicyTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AndroidDoesNotAutofocusPanels(bool android, bool expected) =>
        Assert.Equal(expected, KeyboardFocusPolicy.AutoFocusOnOpen(android));

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public void EditableTouchIsRequiredOnAndroid(bool android, bool touched, bool expected) =>
        Assert.Equal(expected, KeyboardFocusPolicy.FocusEditableOnTouch(android, touched));
}
