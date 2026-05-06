using CubicOdysseyVault.Core;
using Xunit;

namespace CubicOdysseyVault.Tests;

public class ConstantsTests
{
    [Fact]
    public void CubicOdysseyAppId_IsCorrect()
    {
        Assert.Equal(3400000, Constants.CubicOdysseyAppId);
    }
}
