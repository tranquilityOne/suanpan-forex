using Suanpan.Core.Sys.Enums;

namespace Suanpan.Core.Sys.Rules;

/// <summary>
/// 密码通过后的下一步（sys 设计 §5.1）：先改密码，再绑定 TOTP，最后输入验证码。
/// 这些步骤都在待完成 Cookie 下进行；每一步都按账号当前的状态重新判断，不能跳过前面的步骤。
/// </summary>
public static class SignInStepRule
{
    public static SignInStep AfterPassword(bool mustChangePassword, bool totpBound)
        => mustChangePassword ? SignInStep.ChangePassword
            : !totpBound ? SignInStep.SetupTotp
            : SignInStep.VerifyTotp;
}
