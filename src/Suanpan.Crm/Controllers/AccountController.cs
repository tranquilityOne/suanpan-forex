using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Models.Account;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Auth.Models;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;

namespace Suanpan.Crm.Controllers;

/// <summary>
/// 账号自助页面（sys 设计 §5.1、§9.2）：登录 →（必须时）改密码 →（未绑定时）绑定 TOTP → 输入验证码 → 首页。
/// 密码通过后签发待完成 Cookie，全部完成后才签发正式 Cookie；中间每一步都按账号当前的状态跳转，不能跳步。
/// 已登录后也可以主动改密码、重新绑定 TOTP：完成后用新的安全戳重签当前 Cookie，本会话不受影响，其他设备上的会话失效。
/// 登录、验证码、退出只写登录日志；改密码、绑定 TOTP 是账号变更，另记操作日志。
/// 开发环境开启 <see cref="TotpSkipSwitch"/> 时，已绑定账号到了输入验证码这一步直接完成登录。
/// </summary>
public sealed class AccountController(IAuthService authService, TotpSkipSwitch totpSkip) : Controller
{
    private const string SignedInOrPending = AuthSchemes.Crm + "," + AuthSchemes.CrmPending;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
        => IsSignedIn ? LocalRedirect(SafeReturnUrl(returnUrl)) : View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.PasswordSignInAsync(model.Username!, model.Password!, Client, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        await HttpContext.SignOutAsync(AuthSchemes.Crm);
        await SignInAsync(AuthSchemes.CrmPending, result.User!);
        return await ContinueAsync(result, model.ReturnUrl, ct);
    }

    [Authorize(AuthenticationSchemes = SignedInOrPending)]
    [HttpGet]
    public async Task<IActionResult> ChangePassword(string? returnUrl, CancellationToken ct)
    {
        if (!IsSignedIn && await RedirectUnlessAtAsync(SignInStep.ChangePassword, returnUrl, ct) is { } redirect)
        {
            return redirect;
        }

        return View(new ChangePasswordViewModel { ReturnUrl = returnUrl, IsSignedIn = IsSignedIn });
    }

    [Authorize(AuthenticationSchemes = SignedInOrPending)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog("账号", "修改密码")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        var signedIn = IsSignedIn;
        model.IsSignedIn = signedIn;
        if (signedIn && string.IsNullOrEmpty(model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "请填写当前密码");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.ChangePasswordAsync(
            UserId, signedIn ? model.CurrentPassword : null, model.NewPassword!, signedIn, Client, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        if (signedIn)
        {
            await SignInAsync(AuthSchemes.Crm, result.User!);
            TempData["Message"] = "密码已修改，其他设备上的登录已失效";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        await SignInAsync(AuthSchemes.CrmPending, result.User!);
        return await ContinueAsync(result, model.ReturnUrl, ct);
    }

    [Authorize(AuthenticationSchemes = SignedInOrPending)]
    [HttpGet]
    public async Task<IActionResult> TwoFactorSetup(string? returnUrl, CancellationToken ct)
    {
        if (!IsSignedIn && await RedirectUnlessAtAsync(SignInStep.SetupTotp, returnUrl, ct) is { } redirect)
        {
            return redirect;
        }

        return View(BuildSetupModel(authService.CreateTotpSetup(UserName), returnUrl));
    }

    [Authorize(AuthenticationSchemes = SignedInOrPending)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [OpLog("账号", "绑定二次验证")]
    public async Task<IActionResult> TwoFactorSetup(TwoFactorSetupViewModel model, CancellationToken ct)
    {
        var signedIn = IsSignedIn;
        if (ModelState.IsValid)
        {
            var result = await authService.ConfirmTotpSetupAsync(UserId, model.ProtectedSecret!, model.Code!, signedIn, Client, ct);
            if (result.Success)
            {
                if (!signedIn)
                {
                    return await CompleteSignInAsync(result.User!, model.ReturnUrl);
                }

                await SignInAsync(AuthSchemes.Crm, result.User!);
                TempData["Message"] = "二次验证已重新绑定，其他设备上的登录已失效";
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        // 显示同一个二维码，不用重新扫码；密钥无效时换一个新的
        var setup = authService.RestoreTotpSetup(model.ProtectedSecret ?? string.Empty, UserName)
                    ?? authService.CreateTotpSetup(UserName);
        return View(BuildSetupModel(setup, model.ReturnUrl));
    }

    [Authorize(AuthenticationSchemes = AuthSchemes.CrmPending)]
    [HttpGet]
    public async Task<IActionResult> TwoFactor(string? returnUrl, CancellationToken ct)
        => await RedirectUnlessAtAsync(SignInStep.VerifyTotp, returnUrl, ct) ?? View(new TwoFactorViewModel { ReturnUrl = returnUrl });

    [Authorize(AuthenticationSchemes = AuthSchemes.CrmPending)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactor(TwoFactorViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.VerifyTotpAsync(UserId, model.Code!, Client, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        return await CompleteSignInAsync(result.User!, model.ReturnUrl);
    }

    /// <summary>退出；登录过程中（待完成 Cookie）也可以用它放弃登录</summary>
    [Authorize(AuthenticationSchemes = SignedInOrPending)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await authService.LogoutAsync(UserId, UserName, Client, ct);
        await HttpContext.SignOutAsync(AuthSchemes.Crm);
        await HttpContext.SignOutAsync(AuthSchemes.CrmPending);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// <summary>当前请求是否带着正式 Cookie（已完成登录）；否则是待完成 Cookie</summary>
    private bool IsSignedIn => User.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == AuthSchemes.Crm);

    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);

    private string UserName => User.FindFirstValue(ClaimTypes.Name)!;

    private LoginClient Client => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

    /// <summary>登录过程中账号状态不在这一步时，跳到它应进行的步骤；账号已停用时退回登录页</summary>
    private async Task<IActionResult?> RedirectUnlessAtAsync(SignInStep step, string? returnUrl, CancellationToken ct)
    {
        var progress = await authService.GetProgressAsync(UserId, ct);
        if (!progress.Success)
        {
            await HttpContext.SignOutAsync(AuthSchemes.CrmPending);
            return RedirectToAction(nameof(Login));
        }

        return progress.Next == step ? null : GoTo(progress.Next, returnUrl);
    }

    /// <summary>
    /// 密码或强制改密码之后进入下一步。下一步是输入验证码、并且开发环境开启了跳过时，直接完成登录；
    /// 跳过失败（如账号刚被锁定）时照常进入验证码页。账号取自上一步的结果：本次请求刚签发的 Cookie 要到下一次请求才生效
    /// </summary>
    private async Task<IActionResult> ContinueAsync(AuthResult previous, string? returnUrl, CancellationToken ct)
    {
        if (previous.Next == SignInStep.VerifyTotp && totpSkip.Enabled
            && await authService.SkipTotpAsync(previous.User!.Id, Client, ct) is { Success: true } skipped)
        {
            return await CompleteSignInAsync(skipped.User!, returnUrl);
        }

        return GoTo(previous.Next, returnUrl);
    }

    private IActionResult GoTo(SignInStep next, string? returnUrl) => next switch
    {
        SignInStep.ChangePassword => RedirectToAction(nameof(ChangePassword), new { returnUrl }),
        SignInStep.SetupTotp => RedirectToAction(nameof(TwoFactorSetup), new { returnUrl }),
        SignInStep.VerifyTotp => RedirectToAction(nameof(TwoFactor), new { returnUrl }),
        _ => LocalRedirect(SafeReturnUrl(returnUrl)),
    };

    private async Task<IActionResult> CompleteSignInAsync(AuthUser user, string? returnUrl)
    {
        await HttpContext.SignOutAsync(AuthSchemes.CrmPending);
        await SignInAsync(AuthSchemes.Crm, user);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    /// <summary>签发 Cookie：Claims 带用户 id、登录名、姓名和安全戳（sys 设计 §5.2）</summary>
    private Task SignInAsync(string scheme, AuthUser user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(SuanpanClaimTypes.RealName, user.RealName),
            new Claim(SuanpanClaimTypes.SecurityStamp, user.SecurityStamp),
        ], scheme);
        return HttpContext.SignInAsync(scheme, new ClaimsPrincipal(identity));
    }

    private TwoFactorSetupViewModel BuildSetupModel(TotpSetup setup, string? returnUrl) => new()
    {
        ProtectedSecret = setup.ProtectedSecret,
        SecretForDisplay = string.Join(' ', setup.Secret.Chunk(4).Select(chunk => new string(chunk))),
        QrCodeDataUri = "data:image/png;base64," + Convert.ToBase64String(QrCodeHelper.ToPng(setup.OtpauthUri)),
        ReturnUrl = returnUrl,
        IsSignedIn = IsSignedIn,
    };

    private string SafeReturnUrl(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
