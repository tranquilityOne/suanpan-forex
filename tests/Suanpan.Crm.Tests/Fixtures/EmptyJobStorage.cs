using System.Reflection;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Suanpan.Crm.Tests.Fixtures;

/// <summary>
/// 没有任何任务的 Hangfire 存储，让面板页面不连数据库也能渲染。
/// 存储接口的方法很多，用 DispatchProxy 统一返回空值：数字为 0，集合为空，其余为 null。
/// </summary>
public sealed class EmptyJobStorage : JobStorage
{
    public override IMonitoringApi GetMonitoringApi() => EmptyProxy.Create<IMonitoringApi>();

    public override IStorageConnection GetConnection() => EmptyProxy.Create<IStorageConnection>();

    public class EmptyProxy : DispatchProxy
    {
        public static T Create<T>() where T : class => Create<T, EmptyProxy>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod is null ? null : EmptyValue(targetMethod.ReturnType);

        private static object? EmptyValue(Type type)
        {
            if (type == typeof(void) || type == typeof(string))
            {
                return null;
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(JobList<>))
            {
                var itemType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), type.GetGenericArguments()[0]);
                return Activator.CreateInstance(type, Array.CreateInstance(itemType, 0));
            }

            if (type.IsInterface && type.IsGenericType)
            {
                var arguments = type.GetGenericArguments();
                var concrete = arguments.Length == 2 ? typeof(Dictionary<,>) : typeof(List<>);
                return Activator.CreateInstance(concrete.MakeGenericType(arguments));
            }

            return type.GetConstructor(Type.EmptyTypes) is null ? null : Activator.CreateInstance(type);
        }
    }
}
