using WebApp.Domain.Common;

namespace WebApp.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Ok_WithValue_ExposesValue() => Assert.Equal(42, Result.Ok(42).Value);

    [Fact]
    public void Fail_ReadingValue_Throws() =>
        Assert.Throws<InvalidOperationException>(() => Result.Fail<int>("boom").Value);
}
