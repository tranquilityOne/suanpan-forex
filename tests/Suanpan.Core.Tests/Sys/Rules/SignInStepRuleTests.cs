using Suanpan.Core.Sys.Enums;
using Suanpan.Core.Sys.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Sys.Rules;

public class SignInStepRuleTests
{
    [Theory]
    [InlineData(true, false, SignInStep.ChangePassword)]   // 新账号：先改密码，再绑定 TOTP
    [InlineData(false, false, SignInStep.SetupTotp)]       // 改完密码，或管理员重置了二次验证
    [InlineData(false, true, SignInStep.VerifyTotp)]
    public void NextStepAfterPassword(bool mustChangePassword, bool totpBound, SignInStep expected)
        => Assert.Equal(expected, SignInStepRule.AfterPassword(mustChangePassword, totpBound));
}
