using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginProgressTests
{
    [Fact]
    public void Starts_by_waiting_for_the_phone()
    {
        Assert.Equal(LoginStep.PhoneRequired, new LoginProgress().Step);
    }

    [Fact]
    public void The_third_failure_uses_up_the_last_try()
    {
        var progress = new LoginProgress();

        Assert.False(progress.CountFailure());
        Assert.False(progress.CountFailure());
        Assert.True(progress.CountFailure());
    }
}
