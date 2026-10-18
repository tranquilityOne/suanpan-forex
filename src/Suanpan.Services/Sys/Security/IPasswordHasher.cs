namespace Suanpan.Services.Sys.Security;

/// <summary>密码哈希</summary>
public interface IPasswordHasher
{
    /// <summary>生成密码哈希（含盐和迭代次数的自描述格式）</summary>
    string Hash(string password);

    /// <summary>校验密码；哈希格式损坏时返回 false，不抛异常</summary>
    bool Verify(string password, string passwordHash);
}
